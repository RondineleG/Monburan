#!/usr/bin/env python3
"""Simulador local do Mercado Pago para desenvolvimento (nunca usar fora da máquina local).

Implementa o contrato mínimo que o MercadoPagoBillingService consome:
  POST /preapproval            -> cria recorrência pendente e devolve init_point local
  GET  /preapproval/{id}       -> consulta da recorrência (status/external_reference)
  PUT  /preapproval/{id}       -> cancelamento da recorrência ({status: cancelled})
  GET  /authorized_payments/{id} -> consulta da cobrança (status/valor/moeda/preapproval_id)
  GET  /checkout/{id}          -> tela de pagamento da simulação
  POST /checkout/{id}/pay      -> aprova e dispara webhook subscription_authorized_payment
  POST /checkout/{id}/cancel   -> cancela e dispara webhook subscription_preapproval
  GET  /state                  -> estado em memória (debug)

Configuração no app (Development): Billing:MercadoPago:SimulateLocally=true e
Billing:MercadoPago:ApiBaseUrl=http://localhost:5445. O segredo do webhook é o
mesmo em Billing:MercadoPago:WebhookSecret (env MP_FAKE_SECRET aqui).

Uso: python tools/fake-mercadopago.py [porta]
"""

import hashlib
import hmac
import json
import os
import sys
import urllib.request
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 5445
SECRET = os.environ.get("MP_FAKE_SECRET", "local-webhook-secret-123")
BASE = f"http://localhost:{PORT}"

preapprovals = {}  # id -> dict
payments = {}      # id -> dict


def send_webhook(notification_url, event_type, data_id):
    """Replica o formato do webhook do MP: assinatura HMAC no cabeçalho x-signature."""
    ts = str(int(__import__("time").time()))
    request_id = str(uuid.uuid4())
    manifest = f"id:{str(data_id).lower()};request-id:{request_id};ts:{ts};"
    signature = hmac.new(SECRET.encode(), manifest.encode(), hashlib.sha256).hexdigest()
    body = json.dumps({"type": event_type, "data": {"id": str(data_id)}}).encode()
    url = f"{notification_url}?data.id={data_id}"
    req = urllib.request.Request(
        url,
        data=body,
        headers={
            "Content-Type": "application/json",
            "x-signature": f"ts={ts},v1={signature}",
            "x-request-id": request_id,
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=15) as res:
            print(f"[webhook] {event_type} {data_id} -> HTTP {res.status}")
    except Exception as exc:  # o app pode estar reciclando; o log ajuda a diagnosticar
        print(f"[webhook] FALHOU {event_type} {data_id}: {exc}")


CHECKOUT_PAGE = """<!doctype html>
<html lang="pt-BR"><head><meta charset="utf-8"><title>Mercado Pago — simulação local</title>
<style>
body {{ font-family: system-ui, sans-serif; background:#f5f5f5; display:flex; justify-content:center; padding-top:60px; }}
.card {{ background:#fff; border-radius:12px; box-shadow:0 2px 12px #0002; padding:32px; max-width:420px; width:100%; }}
.badge {{ background:#ffe9a8; color:#5f4b00; border-radius:6px; padding:4px 10px; font-size:12px; font-weight:600; }}
h1 {{ font-size:20px; margin:12px 0 4px; }}
.amount {{ font-size:32px; font-weight:700; margin:16px 0; }}
button {{ border:0; border-radius:8px; padding:12px 16px; font-size:15px; cursor:pointer; width:100%; margin-top:8px; }}
.pay {{ background:#3483fa; color:#fff; }}
.cancel {{ background:#eee; color:#444; }}
small {{ color:#666; display:block; margin-top:16px; }}
</style></head><body><div class="card">
<span class="badge">SIMULAÇÃO LOCAL — NENHUMA COBRANÇA REAL</span>
<h1>{reason}</h1>
<div>Assinatura recorrente · {payer}</div>
<div class="amount">R$ {amount}</div>
<form method="post" action="/checkout/{pid}/pay"><button class="pay" type="submit">Aprovar pagamento</button></form>
<form method="post" action="/checkout/{pid}/cancel"><button class="cancel" type="submit">Cancelar assinatura</button></form>
<small>preapproval {pid}</small>
</div></body></html>"""


class Handler(BaseHTTPRequestHandler):
    def _json(self, code, obj):
        body = json.dumps(obj).encode()
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def _read_body(self):
        # O HttpClient do .NET pode enviar Transfer-Encoding: chunked (sem Content-Length)
        if "chunked" in (self.headers.get("Transfer-Encoding") or "").lower():
            chunks = bytearray()
            while True:
                size_line = self.rfile.readline().strip()
                if not size_line:
                    continue
                size = int(size_line.split(b";")[0], 16)
                if size == 0:
                    while self.rfile.readline().strip() != b"":
                        pass
                    break
                chunks += self.rfile.read(size)
                self.rfile.read(2)
            raw = bytes(chunks)
        else:
            length = int(self.headers.get("Content-Length") or 0)
            raw = self.rfile.read(length) if length else b""
        try:
            return json.loads(raw or b"{}")
        except json.JSONDecodeError:
            return {}

    def _redirect(self, url):
        self.send_response(303)
        self.send_header("Location", url)
        self.end_headers()

    def log_message(self, fmt, *args):
        print(f"[mp-fake] {fmt % args}")

    def do_POST(self):
        path = urlparse(self.path).path.rstrip("/")
        if path == "/preapproval":
            data = self._read_body()
            pid = f"fake-{uuid.uuid4()}"
            recurring = data.get("auto_recurring") or {}
            preapprovals[pid] = {
                "id": pid,
                "status": "pending",
                "external_reference": data.get("external_reference"),
                "reason": data.get("reason"),
                "payer_email": data.get("payer_email"),
                "back_url": data.get("back_url"),
                "notification_url": data.get("notification_url"),
                "transaction_amount": recurring.get("transaction_amount", 0),
                "currency_id": recurring.get("currency_id", "BRL"),
            }
            print(f"[mp-fake] preapproval criado {pid} ref={data.get('external_reference')}")
            return self._json(201, {"id": pid, "init_point": f"{BASE}/checkout/{pid}"})

        if path.startswith("/checkout/") and path.endswith("/pay"):
            pid = path.split("/")[2]
            pre = preapprovals.get(pid)
            if not pre:
                return self._json(404, {"error": "preapproval desconhecido"})
            pre["status"] = "authorized"
            pay_id = f"pay-{uuid.uuid4()}"
            payments[pay_id] = {
                "id": pay_id,
                "status": "approved",
                "transaction_amount": pre["transaction_amount"],
                "currency_id": pre["currency_id"],
                "preapproval_id": pid,
            }
            if pre.get("notification_url"):
                send_webhook(pre["notification_url"], "subscription_authorized_payment", pay_id)
            return self._redirect(pre.get("back_url") or "/")

        if path.startswith("/checkout/") and path.endswith("/cancel"):
            pid = path.split("/")[2]
            pre = preapprovals.get(pid)
            if not pre:
                return self._json(404, {"error": "preapproval desconhecido"})
            pre["status"] = "cancelled"
            if pre.get("notification_url"):
                send_webhook(pre["notification_url"], "subscription_preapproval", pid)
            return self._redirect(pre.get("back_url") or "/")

        return self._json(404, {"error": "rota desconhecida"})

    def do_GET(self):
        path = urlparse(self.path).path.rstrip("/")
        if path == "/state":
            return self._json(200, {"preapprovals": preapprovals, "payments": payments})

        if path.startswith("/preapproval/"):
            pre = preapprovals.get(path.split("/")[2])
            if not pre:
                return self._json(404, {"error": "preapproval desconhecido"})
            return self._json(200, {
                "id": pre["id"],
                "status": pre["status"],
                "external_reference": pre["external_reference"],
            })

        if path.startswith("/authorized_payments/"):
            pay = payments.get(path.split("/")[2])
            if not pay:
                return self._json(404, {"error": "pagamento desconhecido"})
            return self._json(200, pay)

        if path.startswith("/checkout/"):
            pid = path.split("/")[2]
            pre = preapprovals.get(pid)
            if not pre:
                return self._json(404, {"error": "preapproval desconhecido"})
            amount = f"{float(pre['transaction_amount']):,.2f}".replace(",", "X").replace(".", ",").replace("X", ".")
            body = CHECKOUT_PAGE.format(
                reason=pre.get("reason") or "Assinatura",
                payer=pre.get("payer_email") or "",
                amount=amount,
                pid=pid,
            ).encode()
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return

        return self._json(404, {"error": "rota desconhecida"})

    def do_PUT(self):
        path = urlparse(self.path).path.rstrip("/")
        if path.startswith("/preapproval/"):
            pre = preapprovals.get(path.split("/")[2])
            if not pre:
                return self._json(404, {"error": "preapproval desconhecido"})
            status = (self._read_body().get("status") or "").lower()
            if status in ("cancelled", "paused", "authorized", "pending"):
                pre["status"] = status
                # O MP real notifica a mudança de estado da recorrência via webhook
                if pre.get("notification_url") and status in ("cancelled", "paused"):
                    send_webhook(pre["notification_url"], "subscription_preapproval", pre["id"])
            return self._json(200, {"id": pre["id"], "status": pre["status"]})
        return self._json(404, {"error": "rota desconhecida"})


if __name__ == "__main__":
    print(f"[mp-fake] Mercado Pago simulado em {BASE} (segredo: {'custom' if os.environ.get('MP_FAKE_SECRET') else 'padrão'})")
    ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
