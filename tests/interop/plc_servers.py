"""Independent reference servers for MKR read-only acquisition tests.

Values are encoded by third-party libraries, never by MKR:
  * Modbus TCP: pymodbus 3.8.6 (ModbusTcpServer, convert_to_registers),
  * S7comm:    Snap7 C library through python-snap7 2.0.2 (Server, snap7.util setters).
The script writes a JSON manifest describing each case and the expected engineering values,
then serves until terminated. Usage: plc_servers.py MANIFEST MODBUS_PORT S7_PORT
"""
import asyncio
import ctypes
import json
import sys
import threading

import snap7
from snap7.type import SrvArea
from snap7.util import set_dint, set_int, set_real
from pymodbus.client.mixin import ModbusClientMixin
from pymodbus.datastore import ModbusSequentialDataBlock, ModbusServerContext, ModbusSlaveContext
from pymodbus.server import StartAsyncTcpServer

T = ModbusClientMixin.DATATYPE
REGISTERS = 1000
# Snap7 shares these buffers by pointer: keep them alive for the whole run.
KEEP_ALIVE = []


def registers(value, data_type, word_order):
    return ModbusClientMixin.convert_to_registers(value, data_type, word_order=word_order)


def modbus_cases():
    holding, inputs, cases = [0] * REGISTERS, [0] * REGISTERS, []

    def place(table, offset, words):
        table[offset:offset + len(words)] = words

    # REAL32, low word first (CDAB) — common on Schneider M340/M580 %MW pairs.
    for offset, value in ((100, 45.25), (102, -3.5), (104, 61.875)):
        place(holding, offset, registers(value, T.FLOAT32, "little"))
    cases.append(dict(name="modbus_real_cdab", table="HoldingRegisters", order="CDAB",
                      signals=[dict(offset=o, type="Real32") for o in (100, 102, 104)],
                      expected=[45.25, -3.5, 61.875]))
    # REAL32, high word first (ABCD).
    for offset, value in ((200, 1.0e-3), (202, 12345.5), (204, -0.0625)):
        place(holding, offset, registers(value, T.FLOAT32, "big"))
    cases.append(dict(name="modbus_real_abcd", table="HoldingRegisters", order="ABCD",
                      signals=[dict(offset=o, type="Real32") for o in (200, 202, 204)],
                      expected=[1.0e-3, 12345.5, -0.0625]))
    # INT16 scaled 0..10000 -> 0..100 %, INT32 and UINT16 raw.
    place(holding, 300, registers(-1234, T.INT16, "big"))
    place(holding, 301, registers(-2000000, T.INT32, "big"))
    place(holding, 303, registers(65000, T.UINT16, "big"))
    cases.append(dict(name="modbus_integers", table="HoldingRegisters", order="ABCD",
                      signals=[dict(offset=300, type="Int16", rawLow=0, rawHigh=10000, engLow=0, engHigh=100),
                               dict(offset=301, type="Int32"), dict(offset=303, type="UInt16")],
                      expected=[-12.34, -2000000, 65000]))
    # UINT32 low word first in the input-register table (function 04).
    place(inputs, 500, registers(4000000000, T.UINT32, "little"))
    place(inputs, 502, registers(7.5, T.FLOAT32, "little"))
    place(inputs, 504, registers(-7, T.INT16, "big"))
    cases.append(dict(name="modbus_input_registers", table="InputRegisters", order="CDAB",
                      signals=[dict(offset=500, type="UInt32"), dict(offset=502, type="Real32"),
                               dict(offset=504, type="Int16")],
                      expected=[4000000000, 7.5, -7]))
    # Signals more than 125 registers apart: several requests per reading.
    place(holding, 10, registers(1.5, T.FLOAT32, "big"))
    place(holding, 600, registers(2.5, T.FLOAT32, "big"))
    place(holding, 990, registers(3.5, T.FLOAT32, "big"))
    cases.append(dict(name="modbus_scattered", table="HoldingRegisters", order="ABCD",
                      signals=[dict(offset=o, type="Real32") for o in (10, 600, 990)],
                      expected=[1.5, 2.5, 3.5]))
    return holding, inputs, cases


def s7_cases(server):
    db10 = (ctypes.c_ubyte * 32)()
    db11 = (ctypes.c_ubyte * 16)()
    merker = (ctypes.c_ubyte * 64)()
    for offset, value in ((0, 52.5), (4, 48.25), (8, -12.75)):
        view = bytearray(4)
        set_real(view, 0, value)
        ctypes.memmove(ctypes.addressof(db10) + offset, bytes(view), 4)
    view = bytearray(16)
    set_int(view, 0, 13824)        # Siemens analog raw 0..27648 -> 50 %
    set_dint(view, 2, -123456)
    set_int(view, 6, -32768)
    ctypes.memmove(ctypes.addressof(db11), bytes(view), 16)
    view = bytearray(12)
    for i, value in enumerate((0.25, 99.5, 1.0e6)):
        set_real(view, 4 * i, value)
    ctypes.memmove(ctypes.addressof(merker) + 20, bytes(view), 12)
    KEEP_ALIVE.extend((db10, db11, merker))
    server.register_area(SrvArea.DB, 10, db10)
    server.register_area(SrvArea.DB, 11, db11)
    server.register_area(SrvArea.MK, 0, merker)
    return [
        dict(name="s7_db_real", area="DataBlock", db=10,
             signals=[dict(offset=o, type="Real32") for o in (0, 4, 8)], expected=[52.5, 48.25, -12.75]),
        dict(name="s7_db_integers", area="DataBlock", db=11,
             signals=[dict(offset=0, type="Int16", rawLow=0, rawHigh=27648, engLow=0, engHigh=100),
                      dict(offset=2, type="Int32"), dict(offset=6, type="Int16")],
             expected=[50.0, -123456, -32768]),
        dict(name="s7_merker_real", area="Merker", db=0,
             signals=[dict(offset=o, type="Real32") for o in (20, 24, 28)], expected=[0.25, 99.5, 1.0e6]),
        dict(name="s7_missing_db", area="DataBlock", db=99,
             signals=[dict(offset=o, type="Real32") for o in (0, 4, 8)], expectedError="s7_item_0A"),
        dict(name="s7_out_of_range", area="DataBlock", db=11,
             signals=[dict(offset=o, type="Real32") for o in (0, 4, 14)], expectedError="s7_item_05"),
    ]


async def main(manifest, modbus_port, s7_port):
    holding, inputs, modbus = modbus_cases()
    # zero_mode=False: request address k maps to block address k + 1, hence start=1.
    store = ModbusSlaveContext(hr=ModbusSequentialDataBlock(1, holding), ir=ModbusSequentialDataBlock(1, inputs))
    context = ModbusServerContext(slaves={7: store}, single=False)
    server = snap7.server.Server(log=False)
    s7 = s7_cases(server)
    server.start(tcp_port=s7_port)
    with open(manifest, "w", encoding="utf-8") as stream:
        json.dump(dict(modbusPort=modbus_port, modbusUnit=7, s7Port=s7_port, modbus=modbus, s7=s7,
                       libraries=dict(pymodbus="3.8.6", python_snap7="2.0.2")), stream)
    await StartAsyncTcpServer(context=context, address=("127.0.0.1", modbus_port))


if __name__ == "__main__":
    asyncio.run(main(sys.argv[1], int(sys.argv[2]), int(sys.argv[3])))
