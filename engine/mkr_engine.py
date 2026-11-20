"""MKR local process entry point."""
import json
import re
import sys

ENGINE_VERSION = "0.1.0"
MAX_REQUEST_BYTES = 8192


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
    elif request["operation"] != "ping":
        code = "unsupported_operation"
    elif request["payload"]:
        code = "invalid_request"
    else:
        return {**response, "result": {"status": "ready"}}

    return {**response, "error": {"code": code}}


if __name__ == "__main__":
    response = respond(sys.stdin.buffer.read(MAX_REQUEST_BYTES + 1))
    sys.stdout.buffer.write(json.dumps(response, separators=(",", ":")).encode("utf-8"))
