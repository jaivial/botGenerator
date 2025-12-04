"""
Mock UAZAPI Server for WhatsApp Bot Testing

This server mimics the UAZAPI endpoints that the BotGenerator uses:
- POST /send/text     - Send text messages
- POST /send/menu     - Send buttons/menus
- POST /message/find  - Get message history

It captures all outgoing messages so they can be inspected by tests.
"""

from fastapi import FastAPI, Request, Header
from fastapi.middleware.cors import CORSMiddleware
from datetime import datetime
from typing import Optional
import json
import uuid

app = FastAPI(
    title="Mock UAZAPI Server",
    description="Mock server for testing WhatsApp bot conversations",
    version="1.0.0"
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Storage for captured messages and simulated history
captured_messages: list[dict] = []
simulated_history: dict[str, list[dict]] = {}  # phone -> messages


@app.post("/send/text")
async def send_text(request: Request, token: Optional[str] = Header(None)):
    """Mock endpoint for sending text messages (matches UAZAPI /send/text)"""
    body = await request.json()

    message_id = str(uuid.uuid4())
    timestamp = datetime.now()

    captured = {
        "id": message_id,
        "type": "text",
        "timestamp": timestamp.isoformat(),
        "timestamp_unix": int(timestamp.timestamp()),
        "phone": body.get("number"),
        "text": body.get("text"),
        "raw_payload": body,
        "token_present": token is not None
    }

    captured_messages.append(captured)

    # Also add to simulated history for this phone
    phone = body.get("number", "")
    if phone not in simulated_history:
        simulated_history[phone] = []

    simulated_history[phone].append({
        "id": message_id,
        "chatid": f"{phone}@s.whatsapp.net",
        "text": body.get("text"),
        "fromMe": True,
        "timestamp": int(timestamp.timestamp()),
        "type": "text"
    })

    print(f"[CAPTURED TEXT] To: {phone} | Message: {body.get('text', '')[:80]}...")

    # Return success response like UAZAPI would
    return {
        "success": True,
        "messageId": message_id,
        "status": "sent"
    }


@app.post("/send/menu")
async def send_menu(request: Request, token: Optional[str] = Header(None)):
    """Mock endpoint for sending buttons/menus (matches UAZAPI /send/menu)"""
    body = await request.json()

    message_id = str(uuid.uuid4())
    timestamp = datetime.now()

    menu_type = body.get("type", "unknown")

    captured = {
        "id": message_id,
        "type": f"menu_{menu_type}",
        "timestamp": timestamp.isoformat(),
        "timestamp_unix": int(timestamp.timestamp()),
        "phone": body.get("number"),
        "text": body.get("text"),
        "menu_type": menu_type,
        "choices": body.get("choices"),
        "sections": body.get("sections"),
        "button_text": body.get("buttonText"),
        "footer_text": body.get("footerText"),
        "raw_payload": body,
        "token_present": token is not None
    }

    captured_messages.append(captured)

    # Add to simulated history
    phone = body.get("number", "")
    if phone not in simulated_history:
        simulated_history[phone] = []

    simulated_history[phone].append({
        "id": message_id,
        "chatid": f"{phone}@s.whatsapp.net",
        "text": body.get("text"),
        "fromMe": True,
        "timestamp": int(timestamp.timestamp()),
        "type": menu_type
    })

    print(f"[CAPTURED MENU] To: {phone} | Type: {menu_type} | Text: {body.get('text', '')[:50]}...")

    return {
        "success": True,
        "messageId": message_id,
        "status": "sent"
    }


@app.post("/message/find")
async def find_messages(request: Request, token: Optional[str] = Header(None)):
    """Mock endpoint for retrieving message history (matches UAZAPI /message/find)"""
    body = await request.json()

    chatid = body.get("chatid", "")
    limit = body.get("limit", 20)

    # Extract phone from chatid (e.g., "34612345678@s.whatsapp.net" -> "34612345678")
    phone = chatid.replace("@s.whatsapp.net", "")

    # Return simulated history for this phone
    history = simulated_history.get(phone, [])

    # Return most recent messages up to limit
    return history[-limit:]


# === Test Control Endpoints ===

@app.get("/captured")
async def get_captured():
    """Get all captured outgoing messages"""
    return {
        "count": len(captured_messages),
        "messages": captured_messages
    }


@app.get("/captured/latest")
async def get_latest_captured(count: int = 1):
    """Get the latest N captured messages"""
    return {
        "count": min(count, len(captured_messages)),
        "messages": captured_messages[-count:] if captured_messages else []
    }


@app.get("/captured/phone/{phone}")
async def get_captured_for_phone(phone: str):
    """Get captured messages for a specific phone number"""
    phone_messages = [m for m in captured_messages if m.get("phone") == phone]
    return {
        "count": len(phone_messages),
        "messages": phone_messages
    }


@app.delete("/captured")
async def clear_captured():
    """Clear all captured messages"""
    captured_messages.clear()
    print("[CLEARED] All captured messages cleared")
    return {"success": True, "message": "Captured messages cleared"}


@app.delete("/history")
async def clear_history():
    """Clear simulated message history"""
    simulated_history.clear()
    print("[CLEARED] Simulated history cleared")
    return {"success": True, "message": "Simulated history cleared"}


@app.delete("/all")
async def clear_all():
    """Clear both captured messages and history"""
    captured_messages.clear()
    simulated_history.clear()
    print("[CLEARED] All data cleared")
    return {"success": True, "message": "All data cleared"}


@app.post("/history/inject")
async def inject_history(request: Request):
    """
    Inject messages into simulated history for testing.
    Useful for setting up conversation context before a test.

    Body format:
    {
        "phone": "34612345678",
        "messages": [
            {"text": "Hola", "fromMe": false},
            {"text": "Hola! Bienvenido", "fromMe": true}
        ]
    }
    """
    body = await request.json()
    phone = body.get("phone", "")
    messages = body.get("messages", [])

    if phone not in simulated_history:
        simulated_history[phone] = []

    base_timestamp = int(datetime.now().timestamp()) - (len(messages) * 60)

    for i, msg in enumerate(messages):
        simulated_history[phone].append({
            "id": str(uuid.uuid4()),
            "chatid": f"{phone}@s.whatsapp.net",
            "text": msg.get("text", ""),
            "fromMe": msg.get("fromMe", False),
            "timestamp": base_timestamp + (i * 60),
            "type": msg.get("type", "text")
        })

    print(f"[INJECTED] {len(messages)} messages for {phone}")
    return {"success": True, "injected_count": len(messages)}


@app.get("/health")
async def health():
    """Health check endpoint"""
    return {
        "status": "healthy",
        "timestamp": datetime.now().isoformat(),
        "captured_count": len(captured_messages),
        "phones_with_history": list(simulated_history.keys())
    }


@app.get("/")
async def root():
    """Root endpoint with API info"""
    return {
        "name": "Mock UAZAPI Server",
        "version": "1.0.0",
        "endpoints": {
            "uazapi_mock": [
                "POST /send/text - Send text message",
                "POST /send/menu - Send buttons/menu",
                "POST /message/find - Get message history"
            ],
            "test_control": [
                "GET /captured - Get all captured messages",
                "GET /captured/latest?count=N - Get latest N messages",
                "GET /captured/phone/{phone} - Get messages for phone",
                "DELETE /captured - Clear captured messages",
                "DELETE /history - Clear simulated history",
                "DELETE /all - Clear everything",
                "POST /history/inject - Inject history for testing",
                "GET /health - Health check"
            ]
        }
    }


if __name__ == "__main__":
    import uvicorn
    print("=" * 60)
    print("Mock UAZAPI Server Starting")
    print("=" * 60)
    print("This server mimics UAZAPI for testing your WhatsApp bot.")
    print("Set WHATSAPP_API_URL=http://localhost:8080 in your .env")
    print("=" * 60)
    uvicorn.run(app, host="0.0.0.0", port=8080)
