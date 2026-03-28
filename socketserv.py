import asyncio

import socketio
import threading
import os
import time
import asyncio
import eventlet
from eventlet.event import Event
sio = socketio.Server()
app = socketio.WSGIApp(sio)

unity_done = asyncio.Event()
unity_response = None

def monitor_keyboard():
    print(">>> [System] Press 'q' then Enter at any time to quit the server.")
    while True:
        user_input = input().strip().lower()
        if user_input == 'q':
            print("Quitting server...")
            os._exit(0) # Force-kill the process and all threads immediately

@sio.event
def connect(sid, environ, auth):
    global unity_sid
    print('connect ', sid, environ['QUERY_STRING'])
    if 'token=UNITY' in environ['QUERY_STRING']:
        unity_sid = sid

@sio.on('MOVE_FWD')
def move_fwd(sid, amount):
    print(f'[sid: {sid}] MOVE_FWD({amount})')
    if unity_sid is not None:
        print(">> Sending to Unity client...")
        sio.emit("MOVE_FWD", (amount,), to=unity_sid)
        return {"success": True}
    else:
        print(">> Unity client not connected.")
        return {"success": False}


@sio.on('MOVE_BCK')
def move_bck(sid, amount):
    print(f'[sid: {sid}] MOVE_BCK({amount})')
    if unity_sid is not None:
        print(">> Sending to Unity client...")
        sio.emit("MOVE_BCK", (amount,), to=unity_sid)
        return {"success": True}
    else:
        print(">> Unity client not connected.")
        return {"success": False}


@sio.on('MOVE_LFT')
def move_lft(sid, amount):
    print(f'[sid: {sid}] MOVE_LFT({amount})')
    if unity_sid is not None:
        print(">> Sending to Unity client...")
        sio.emit("MOVE_LFT", (amount,), to=unity_sid)
        return {"success": True}
    else:
        print(">> Unity client not connected.")
        return {"success": False}


@sio.on('MOVE_RGT')
def move_rgt(sid, amount):
    print(f'[sid: {sid}] MOVE_RGT({amount})')
    if unity_sid is not None:
        print(">> Sending to Unity client...")
        sio.emit("MOVE_RGT", (amount,), to=unity_sid)
        return {"success": True}
    else:
        print(">> Unity client not connected.")
        return {"success": False}


@sio.on('TURN_LFT')
def turn_lft(sid, amount):
    print(f'[sid: {sid}] TURN_LFT({amount})')
    if unity_sid is not None:
        print(">> Sending to Unity client...")
        sio.emit("TURN_LFT", (amount,), to=unity_sid)
        return {"success": True}
    else:
        print(">> Unity client not connected.")
        return {"success": False}


@sio.on('TURN_RGT')
def turn_rgt(sid, amount):
    print(f'[sid: {sid}] TURN_RGT({amount})')
    if unity_sid is not None:
        print(">> Sending to Unity client...")
        sio.emit("TURN_RGT", (amount,), to=unity_sid)
        return {"success": True}
    else:
        print(">> Unity client not connected.")
        return {"success": False}


@sio.on('LOOK_UP')
def look_up(sid, amount):
    print(f'[sid: {sid}] LOOK_UP({amount})')
    if unity_sid is not None:
        print(">> Sending to Unity client...")
        sio.emit("LOOK_UP", (amount,), to=unity_sid)
        return {"success": True}
    else:
        print(">> Unity client not connected.")
        return {"success": False}


@sio.on('LOOK_DOWN')
def look_down(sid, amount):
    print(f'[sid: {sid}] LOOK_DOWN({amount})')
    if unity_sid is not None:
        print(">> Sending to Unity client...")
        sio.emit("LOOK_DOWN", (amount,), to=unity_sid)
        return {"success": True}
    else:
        print(">> Unity client not connected.")
        return {"success": False}


@sio.on('MOVE_TO_ITEM')
def move_to_item(sid, item):
    global unity_response, unity_done
    print(f'[sid: {sid}] MOVE_TO_ITEM({item})')
    if unity_sid is None:
        print(">> Unity client not connected.")
        return {"success": False, "error": "Unity client not connected"}
    print(">> Sending to Unity client...")
    unity_done = Event()
    sio.emit("MOVE_TO_ITEM", item, to=unity_sid)

    try:
        with eventlet.Timeout(10):
            unity_done.wait()  # Wait until the Unity client responds or timeout occurs
            print(f">> Unity response: {unity_response}")
            return {"success": True if unity_response else False}
    except eventlet.timeout.Timeout:
        print(">> Timeout waiting for Unity response.")
        return {"success": False, "error": "Timeout waiting for Unity response"}


@sio.on('PICK_ITEM')
def pick_item(sid, data):
    global unity_response, unity_done

    x = data.get("x")
    y = data.get("y")
    print(f'[sid: {sid}] PICK_ITEM({x}, {y})')
    if unity_sid is None:
        print(">> Unity client not connected.")
        return {"success": False, "error": "Unity client not connected"}
    unity_done = Event()
    print(">> Sending to Unity client...")
    sio.emit("PICK_ITEM",[x,y], to=unity_sid)

    try:
        with eventlet.Timeout(10):
            unity_done.wait()  # Wait until the Unity client responds or timeout occurs
            return {"success": True if unity_response else False}
    except eventlet.timeout.Timeout:
        print(">> Timeout waiting for Unity response.")
        return {"success": False, "error": "Timeout waiting for Unity response"}


@sio.on('UNITY_RESPONSE')
def handle_unity_response(sid, data):
    global unity_response
    print(f">> Received response from Unity client: {data}")
    unity_response = data.get("success", False)  # Assuming Unity sends back a JSON with a 'success' field
    if unity_done:
        unity_done.send()  # Signal that the response has been received


@sio.event
def disconnect(sid, reason):
    print(sid, 'disconnected', reason)


if __name__ == '__main__':
    import eventlet
    import eventlet.wsgi
    threading.Thread(target=monitor_keyboard, daemon=True).start()
    print("Starting Socket.IO server on port 6060...")
    eventlet.wsgi.server(eventlet.listen(('', 6060)), app)