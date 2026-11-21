"""MKR local process entry point."""
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from csv_quality import process_csv

ENGINE_VERSION = "0.9.0"
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
    elif request["operation"] in ("lookup_blocks", "convert_block_settings", "recommend_blocks"):
        from block_catalogue import lookup, convert, recommend
        operation = {"lookup_blocks": lookup, "convert_block_settings": convert,
                     "recommend_blocks": recommend}[request["operation"]]
        try:
            return {**response, "result": operation(request["payload"])}
        except ValueError as error:
            code = str(error)
    elif request["operation"] == "select_strategy":
        from strategy_selection import select
        try:
            return {**response, "result": select(request["payload"])}
        except ValueError as error:
            code = str(error)
    elif request["operation"] == "compare_tunings":
        from tuning_comparison import compare
        try:
            return {**response, "result": compare(request["payload"])}
        except ValueError as error:
            code = str(error)
    elif request["operation"] in ("calculate_pi", "calculate_simc_pid", "simulate_pi", "simulate_open_loop", "simulate_pi_transition"):
        if request["operation"] == "calculate_pi":
            from pi_tuning import calculate
        elif request["operation"] == "calculate_simc_pid":
            from pid_tuning import calculate
        elif request["operation"] == "simulate_pi":
            from pi_simulation import calculate
        elif request["operation"] == "simulate_pi_transition":
            from pi_mode_transition import calculate
        else:
            from open_loop_simulation import calculate
        try:
            result = calculate(request["payload"])
        except ValueError as error:
            code = str(error)
        else:
            return {**response, "result": result}
    elif request["operation"] in ("identify_fopdt", "identify_sopdt", "identify_integrator", "identify_broida", "compare_models"):
        try:
            if request["operation"] == "compare_models":
                from model_comparison import compare as identify
            elif request["operation"] == "identify_fopdt":
                from fopdt import identify
            elif request["operation"] == "identify_sopdt":
                from sopdt import identify
            elif request["operation"] == "identify_broida":
                from broida import identify
            else:
                from integrator import identify
            result = identify(request["payload"])
        except ImportError:
            code = "fit_dependency"
        except ValueError as error:
            error_code = str(error)
            code = error_code if error_code.startswith(("fit_", "csv_", "broida_")) else "fit_data"
        else:
            return {**response, "result": result}
    elif request["operation"] == "simulate_pid_experimental":
        try:
            from pid_simulation import calculate as simulate_pid
            result = simulate_pid(request["payload"])
        except ValueError as error:
            value = str(error)
            code = value if value.startswith(("pid_", "sim_")) else "pid_input"
        else:
            return {**response, "result": result}
    elif request["operation"] == "simulate_pi_combined":
        try:
            from pi_combined_robustness import calculate_combined
            result = calculate_combined(request["payload"])
        except ValueError as error:
            code = str(error) if str(error).startswith(("sim_", "combined_")) else "combined_input"
        else:
            return {**response, "result": result}
    elif request["operation"] == "simulate_pi_robustness":
        try:
            from pi_robustness import calculate_sensitivity
            result = calculate_sensitivity(request["payload"])
        except ValueError as error:
            message = str(error)
            code = message if message.startswith(("sim_", "robust_")) else "robust_input"
        else:
            return {**response, "result": result}
    elif request["operation"] == "preview_trial":
        try:
            from trial_preview import preview
            result = preview(request["payload"])
        except ValueError as error:
            code = str(error) if str(error).startswith(("preview_", "csv_")) else "preview_data"
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
