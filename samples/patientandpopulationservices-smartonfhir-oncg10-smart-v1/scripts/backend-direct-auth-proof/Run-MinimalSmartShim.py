#!/usr/bin/env python3
import argparse
import base64
import fnmatch
import json
import sqlite3
import sys
import time
import urllib.parse
import urllib.request
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Dict, List, Optional, Tuple

from cryptography.hazmat.primitives import hashes, serialization
from cryptography.hazmat.primitives.asymmetric import ec, padding, rsa


def b64url_encode(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).decode("ascii").rstrip("=")


def b64url_decode(value: str) -> bytes:
    padding_len = (4 - len(value) % 4) % 4
    return base64.urlsafe_b64decode(value + ("=" * padding_len))


def load_json(path: Path) -> object:
    return json.loads(path.read_text(encoding="utf-8"))


def write_json_response(handler: BaseHTTPRequestHandler, status: int, payload: object) -> None:
    body = json.dumps(payload, indent=2).encode("utf-8")
    handler.send_response(status)
    handler.send_header("Content-Type", "application/json")
    handler.send_header("Content-Length", str(len(body)))
    handler.end_headers()
    handler.wfile.write(body)


def build_rsa_public_jwk(public_key: rsa.RSAPublicKey) -> Dict[str, str]:
    numbers = public_key.public_numbers()
    n_bytes = numbers.n.to_bytes((numbers.n.bit_length() + 7) // 8, "big")
    e_bytes = numbers.e.to_bytes((numbers.e.bit_length() + 7) // 8, "big")
    jwk = {
        "kty": "RSA",
        "use": "sig",
        "alg": "RS256",
        "n": b64url_encode(n_bytes),
        "e": b64url_encode(e_bytes),
    }
    canonical = json.dumps({"e": jwk["e"], "kty": jwk["kty"], "n": jwk["n"]}, separators=(",", ":"), sort_keys=True).encode("utf-8")
    digest = hashes.Hash(hashes.SHA256())
    digest.update(canonical)
    jwk["kid"] = b64url_encode(digest.finalize())
    return jwk


def load_or_create_signing_key(key_path: Path) -> Tuple[rsa.RSAPrivateKey, Dict[str, str]]:
    if key_path.exists():
        private_key = serialization.load_pem_private_key(key_path.read_bytes(), password=None)
        if not isinstance(private_key, rsa.RSAPrivateKey):
            raise ValueError("Shim signing key must be RSA.")
    else:
        key_path.parent.mkdir(parents=True, exist_ok=True)
        private_key = rsa.generate_private_key(public_exponent=65537, key_size=2048)
        key_path.write_bytes(
            private_key.private_bytes(
                encoding=serialization.Encoding.PEM,
                format=serialization.PrivateFormat.TraditionalOpenSSL,
                encryption_algorithm=serialization.NoEncryption(),
            )
        )
    return private_key, build_rsa_public_jwk(private_key.public_key())


def decode_jwt_unverified(token: str) -> Tuple[Dict[str, object], Dict[str, object], bytes]:
    parts = token.split(".")
    if len(parts) != 3:
        raise ValueError("JWT must have exactly 3 segments.")
    header = json.loads(b64url_decode(parts[0]))
    payload = json.loads(b64url_decode(parts[1]))
    signing_input = f"{parts[0]}.{parts[1]}".encode("utf-8")
    signature = b64url_decode(parts[2])
    return header, payload, signing_input + b"." + signature  # not used directly, but keeps interface stable


def decode_jwt_parts(token: str) -> Tuple[Dict[str, object], Dict[str, object], bytes, bytes]:
    parts = token.split(".")
    if len(parts) != 3:
        raise ValueError("JWT must have exactly 3 segments.")
    header = json.loads(b64url_decode(parts[0]))
    payload = json.loads(b64url_decode(parts[1]))
    signing_input = f"{parts[0]}.{parts[1]}".encode("utf-8")
    signature = b64url_decode(parts[2])
    return header, payload, signing_input, signature


def rsa_public_key_from_jwk(jwk: Dict[str, str]) -> rsa.RSAPublicKey:
    n_value = int.from_bytes(b64url_decode(jwk["n"]), "big")
    e_value = int.from_bytes(b64url_decode(jwk["e"]), "big")
    return rsa.RSAPublicNumbers(e_value, n_value).public_key()


def ec_public_key_from_jwk(jwk: Dict[str, str]) -> ec.EllipticCurvePublicKey:
    curve_name = jwk.get("crv")
    if curve_name != "P-384":
        raise ValueError(f"Unsupported EC curve '{curve_name}'.")
    curve = ec.SECP384R1()
    x_value = int.from_bytes(b64url_decode(jwk["x"]), "big")
    y_value = int.from_bytes(b64url_decode(jwk["y"]), "big")
    return ec.EllipticCurvePublicNumbers(x_value, y_value, curve).public_key()


def verify_signature(header: Dict[str, object], signing_input: bytes, signature: bytes, jwk: Dict[str, str]) -> None:
    alg = header.get("alg")
    if alg == "RS384":
        public_key = rsa_public_key_from_jwk(jwk)
        public_key.verify(signature, signing_input, padding.PKCS1v15(), hashes.SHA384())
        return
    if alg == "ES384":
        public_key = ec_public_key_from_jwk(jwk)
        public_key.verify(signature, signing_input, ec.ECDSA(hashes.SHA384()))
        return
    raise ValueError(f"Unsupported client assertion algorithm '{alg}'.")


def fetch_json_url(url: str) -> Dict[str, object]:
    request = urllib.request.Request(url, headers={"Accept": "application/json"})
    with urllib.request.urlopen(request) as response:
        return json.loads(response.read().decode("utf-8"))


class ReplayStore:
    def __init__(self, db_path: Path) -> None:
        db_path.parent.mkdir(parents=True, exist_ok=True)
        self._conn = sqlite3.connect(str(db_path), check_same_thread=False)
        self._conn.execute(
            """
            CREATE TABLE IF NOT EXISTS used_jti (
                client_id TEXT NOT NULL,
                jti TEXT NOT NULL,
                expires_at INTEGER NOT NULL,
                PRIMARY KEY (client_id, jti)
            )
            """
        )
        self._conn.commit()

    def reserve(self, client_id: str, jti: str, expires_at: int) -> bool:
        now = int(time.time())
        self._conn.execute("DELETE FROM used_jti WHERE expires_at < ?", (now,))
        try:
            self._conn.execute(
                "INSERT INTO used_jti (client_id, jti, expires_at) VALUES (?, ?, ?)",
                (client_id, jti, expires_at),
            )
            self._conn.commit()
            return True
        except sqlite3.IntegrityError:
            return False


class RegistrationStore:
    def __init__(self, registrations_dir: Path) -> None:
        self._registrations_dir = registrations_dir

    def find_by_client_id(self, client_id: str) -> Dict[str, object]:
        if not self._registrations_dir.exists():
            raise FileNotFoundError(f"Registrations directory '{self._registrations_dir}' does not exist.")

        for path in sorted(self._registrations_dir.glob("*.json")):
            registration = load_json(path)
            if not isinstance(registration, dict):
                continue
            if registration.get("clientId") == client_id and registration.get("enabled", True):
                registration["_path"] = str(path)
                return registration
        raise KeyError(f"No enabled registration found for client_id '{client_id}'.")

    def list_supported_scopes(self) -> List[str]:
        scopes: List[str] = []
        if not self._registrations_dir.exists():
            return scopes
        for path in sorted(self._registrations_dir.glob("*.json")):
            registration = load_json(path)
            if not isinstance(registration, dict) or not registration.get("enabled", True):
                continue
            scopes.extend(registration.get("allowedScopes", []))
        return sorted(set(scopes))


class ShimConfig:
    def __init__(self, issuer: str, host: str, port: int, registrations_dir: Path, state_dir: Path, token_lifetime_seconds: int) -> None:
        self.issuer = issuer.rstrip("/")
        self.host = host
        self.port = port
        self.registrations_dir = registrations_dir
        self.state_dir = state_dir
        self.token_lifetime_seconds = token_lifetime_seconds
        self.private_key, self.public_jwk = load_or_create_signing_key(state_dir / "shim-signing-key.pem")
        self.replay_store = ReplayStore(state_dir / "shim-state.db")
        self.registration_store = RegistrationStore(registrations_dir)

    @property
    def jwks(self) -> Dict[str, object]:
        return {"keys": [self.public_jwk]}

    @property
    def token_endpoint(self) -> str:
        return f"{self.issuer}/token"

    @property
    def openid_configuration(self) -> Dict[str, object]:
        return {
            "issuer": self.issuer,
            "jwks_uri": f"{self.issuer}/jwks.json",
            "token_endpoint": self.token_endpoint,
            "grant_types_supported": ["client_credentials"],
            "token_endpoint_auth_methods_supported": ["private_key_jwt"],
            "token_endpoint_auth_signing_alg_values_supported": ["RS384", "ES384"],
            "response_types_supported": ["token"],
            "subject_types_supported": ["public"],
            "scopes_supported": self.registration_store.list_supported_scopes(),
            "capabilities": ["client-confidential-asymmetric"],
        }


def validate_requested_scope(registration: Dict[str, object], requested_scope: str) -> str:
    allowed_scopes = registration.get("allowedScopes", [])
    if not allowed_scopes:
        return requested_scope

    requested_parts = [item for item in requested_scope.split(" ") if item]
    for requested in requested_parts:
        if not any(fnmatch.fnmatch(requested, allowed) or fnmatch.fnmatch(allowed, requested) for allowed in allowed_scopes):
            raise ValueError(f"Requested scope '{requested}' is not allowed for this client.")
    return requested_scope


def issue_token(config: ShimConfig, registration: Dict[str, object], client_id: str, scope: str) -> str:
    now = int(time.time())
    payload: Dict[str, object] = {
        "iss": config.issuer,
        "sub": client_id,
        "aud": registration["fhirAudience"],
        "azp": client_id,
        "appid": client_id,
        "scp": scope,
        "iat": now,
        "nbf": now,
        "exp": now + config.token_lifetime_seconds,
    }
    if registration.get("fhirUser"):
        payload["fhirUser"] = registration["fhirUser"]

    header = {
        "alg": "RS256",
        "typ": "JWT",
        "kid": config.public_jwk["kid"],
    }
    encoded_header = b64url_encode(json.dumps(header, separators=(",", ":"), sort_keys=True).encode("utf-8"))
    encoded_payload = b64url_encode(json.dumps(payload, separators=(",", ":"), sort_keys=True).encode("utf-8"))
    signing_input = f"{encoded_header}.{encoded_payload}".encode("utf-8")
    signature = config.private_key.sign(signing_input, padding.PKCS1v15(), hashes.SHA256())
    return f"{encoded_header}.{encoded_payload}.{b64url_encode(signature)}"


def validate_client_assertion(config: ShimConfig, registration: Dict[str, object], client_assertion: str) -> str:
    header, payload, signing_input, signature = decode_jwt_parts(client_assertion)

    client_id = str(payload.get("sub", ""))
    issuer = str(payload.get("iss", ""))
    if not client_id or issuer != client_id:
        raise ValueError("SMART client_assertion must use identical iss and sub values.")

    if client_id != registration["clientId"]:
        raise ValueError("Registration clientId does not match the client_assertion subject.")

    if str(payload.get("aud", "")) != config.token_endpoint:
        raise ValueError("client_assertion aud must equal the shim token endpoint.")

    exp = int(payload.get("exp", 0))
    if exp <= int(time.time()):
        raise ValueError("client_assertion is expired.")

    jti = str(payload.get("jti", ""))
    if not jti:
        raise ValueError("client_assertion must include jti.")

    jku = header.get("jku")
    registered_jwks_uri = registration.get("jwksUri")
    if jku:
        if not registered_jwks_uri or str(jku) != str(registered_jwks_uri):
            raise ValueError("client_assertion jku does not match the registered jwksUri.")

    if registration.get("jwks"):
        jwks = registration["jwks"]
    elif registered_jwks_uri:
        jwks = fetch_json_url(str(registered_jwks_uri))
    else:
        raise ValueError("Registration must include jwksUri or jwks.")

    alg = str(header.get("alg", ""))
    kid = str(header.get("kid", ""))
    if alg not in {"RS384", "ES384"}:
        raise ValueError(f"Unsupported client_assertion algorithm '{alg}'.")
    if not kid:
        raise ValueError("client_assertion must include kid.")

    candidate_keys = []
    for key in jwks.get("keys", []):
        if key.get("kid") != kid:
            continue
        if alg.startswith("RS") and key.get("kty") != "RSA":
            continue
        if alg.startswith("ES") and key.get("kty") != "EC":
            continue
        candidate_keys.append(key)

    if len(candidate_keys) != 1:
        raise ValueError("Registration JWKS did not resolve to exactly one candidate signing key.")

    verify_signature(header, signing_input, signature, candidate_keys[0])

    if not config.replay_store.reserve(client_id, jti, exp):
        raise ValueError("client_assertion jti has already been used.")

    return client_id


class ShimHandler(BaseHTTPRequestHandler):
    server_version = "MinimalSmartShim/0.1"

    @property
    def config(self) -> ShimConfig:
        return self.server.config  # type: ignore[attr-defined]

    def log_message(self, format: str, *args: object) -> None:
        sys.stderr.write("%s - - [%s] %s\n" % (self.client_address[0], self.log_date_time_string(), format % args))

    def do_GET(self) -> None:
        if self.path == "/.well-known/openid-configuration":
            write_json_response(self, HTTPStatus.OK, self.config.openid_configuration)
            return
        if self.path == "/jwks.json":
            write_json_response(self, HTTPStatus.OK, self.config.jwks)
            return
        if self.path == "/":
            write_json_response(
                self,
                HTTPStatus.OK,
                {
                    "service": "minimal-smart-shim",
                    "issuer": self.config.issuer,
                    "tokenEndpoint": self.config.token_endpoint,
                    "registrationsDirectory": str(self.config.registrations_dir),
                },
            )
            return
        write_json_response(self, HTTPStatus.NOT_FOUND, {"error": "not_found"})

    def do_POST(self) -> None:
        if self.path != "/token":
            write_json_response(self, HTTPStatus.NOT_FOUND, {"error": "not_found"})
            return

        content_type = self.headers.get("Content-Type", "")
        if not content_type.startswith("application/x-www-form-urlencoded"):
            write_json_response(self, HTTPStatus.BAD_REQUEST, {"error": "invalid_request", "error_description": "Content-Type must be application/x-www-form-urlencoded."})
            return

        content_length = int(self.headers.get("Content-Length", "0"))
        body = self.rfile.read(content_length).decode("utf-8")
        form = urllib.parse.parse_qs(body, keep_blank_values=False)
        grant_type = form.get("grant_type", [""])[0]
        assertion_type = form.get("client_assertion_type", [""])[0]
        client_assertion = form.get("client_assertion", [""])[0]
        scope = form.get("scope", [""])[0]

        if grant_type != "client_credentials":
            write_json_response(self, HTTPStatus.BAD_REQUEST, {"error": "unsupported_grant_type", "error_description": "Only client_credentials is supported."})
            return
        if assertion_type != "urn:ietf:params:oauth:client-assertion-type:jwt-bearer":
            write_json_response(self, HTTPStatus.BAD_REQUEST, {"error": "invalid_request", "error_description": "client_assertion_type must be urn:ietf:params:oauth:client-assertion-type:jwt-bearer."})
            return
        if not client_assertion:
            write_json_response(self, HTTPStatus.BAD_REQUEST, {"error": "invalid_request", "error_description": "client_assertion is required."})
            return
        if not scope:
            write_json_response(self, HTTPStatus.BAD_REQUEST, {"error": "invalid_scope", "error_description": "scope is required."})
            return

        try:
            _, payload, _, _ = decode_jwt_parts(client_assertion)
            client_id = str(payload.get("sub", ""))
            registration = self.config.registration_store.find_by_client_id(client_id)
            validate_client_assertion(self.config, registration, client_assertion)
            granted_scope = validate_requested_scope(registration, scope)
            access_token = issue_token(self.config, registration, client_id, granted_scope)
        except KeyError as exc:
            write_json_response(self, HTTPStatus.UNAUTHORIZED, {"error": "invalid_client", "error_description": str(exc)})
            return
        except FileNotFoundError as exc:
            write_json_response(self, HTTPStatus.INTERNAL_SERVER_ERROR, {"error": "server_error", "error_description": str(exc)})
            return
        except ValueError as exc:
            description = str(exc)
            if "scope" in description:
                write_json_response(self, HTTPStatus.BAD_REQUEST, {"error": "invalid_scope", "error_description": description})
                return
            write_json_response(self, HTTPStatus.UNAUTHORIZED, {"error": "invalid_client", "error_description": description})
            return
        except urllib.error.URLError as exc:
            write_json_response(self, HTTPStatus.UNAUTHORIZED, {"error": "invalid_client", "error_description": f"Unable to fetch client JWKS: {exc.reason}"})
            return

        write_json_response(
            self,
            HTTPStatus.OK,
            {
                "token_type": "Bearer",
                "expires_in": self.config.token_lifetime_seconds,
                "access_token": access_token,
                "scope": granted_scope,
            },
        )


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run a minimal SMART-aware shim proof service.")
    parser.add_argument("--issuer", default="http://127.0.0.1:8765", help="Public issuer URL to advertise in discovery and token claims.")
    parser.add_argument("--host", default="127.0.0.1", help="Host interface to bind locally.")
    parser.add_argument("--port", type=int, default=8765, help="Local port to bind.")
    parser.add_argument(
        "--registrations-dir",
        default=str(Path(__file__).resolve().parent / "registrations"),
        help="Directory containing JSON backend-client registrations.",
    )
    parser.add_argument(
        "--state-dir",
        default=str(Path(__file__).resolve().parent / ".shim-state"),
        help="Directory used for shim signing keys and replay state.",
    )
    parser.add_argument("--token-lifetime-seconds", type=int, default=3600, help="Lifetime of issued shim access tokens.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    config = ShimConfig(
        issuer=args.issuer,
        host=args.host,
        port=args.port,
        registrations_dir=Path(args.registrations_dir).resolve(),
        state_dir=Path(args.state_dir).resolve(),
        token_lifetime_seconds=args.token_lifetime_seconds,
    )
    server = ThreadingHTTPServer((config.host, config.port), ShimHandler)
    server.config = config  # type: ignore[attr-defined]
    print(json.dumps({"issuer": config.issuer, "tokenEndpoint": config.token_endpoint, "jwksUri": f"{config.issuer}/jwks.json"}, indent=2))
    server.serve_forever()
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        raise SystemExit(0)
