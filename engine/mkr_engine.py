"""MKR local process entry point."""
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from csv_quality import process_csv

ENGINE_VERSION = "0.3.0"
MAX_REQUEST_BYTES = 2_900_000


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate JSON key")
        result[key] = value
    return result


def respond(raw):
    response = {"version": 1, "requestId": None, "engineVersion": ENGINE_VERSION}
    try:
        if len(raw) > MAX_REQUEST_BYTES:
            raise ValueError("Request exceeds input limit")
        request = json.loads(raw.decode("utf-8"), object_pairs_hook=unique_object)
    except (ValueError, RecursionError):
        return {**response, "error": {"code": "invalid_json"}}

    request_id = request.get("requestId") if isinstance(request, dict) else None
    if isinstance(request_id, str) and re.fullmatch(r"[A-Za-z0-9_-]{1,64}", request_id):
        response["requestId"] = request_id

    if (
        response["requestId"] is None
        or request.keys() != {"version", "requestId", "operation", "payload"}
        or type(request["version"]) is not int
        or not isinstance(request["operation"], str)
        or not isinstance(request["payload"], dict)
    ):
        code = "invalid_request"
    elif request["version"] != 1:
        code = "unsupported_version"
    elif request["operation"] == "identify_fopdt":
        try:
            from fopdt import identify
            result = identify(request["payload"])
        except ImportError:
            code = "fit_dependency"
        except ValueError as error:
            error_code = str(error)
            code = error_code if error_code.startswith(("fit_", "csv_")) else "fit_data"
        else:
            return {**response, "result": result}
    elif request["operation"] in ("inspect_csv", "analyze_csv"):
        try:
            result = process_csv(request["payload"], request["operation"] == "inspect_csv")
        except ValueError as error:
            code = str(error) if str(error).startswith(("csv_", "invalid_request")) else "invalid_request"
        else:
            return {**response, "result": result}
    elif request["operation"] != "ping":
        code = "unsupported_operation"
    elif request["payload"]:
        code = "invalid_request"
    else:
        return {**response, "result": {"status": "ready"}}

    return {**response, "error": {"code": code}}


if __name__ == "__main__":
    response = respond(sys.stdin.buffer.read(MAX_REQUEST_BYTES + 1))
    sys.stdout.buffer.write(json.dumps(response, separators=(",", ":"), allow_nan=False).encode("utf-8"))
