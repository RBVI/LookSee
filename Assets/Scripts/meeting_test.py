#!/usr/bin/python3

def connect_to_looksee(ip_address, port, message_type, json_data, send_prefix = 'LookSeeMeeting'):
    import socket
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.connect((host,port))

    import json
    contents = bytes([message_type]) + bytes(json.dumps(json_data), 'utf-8')
    data = (bytes(send_prefix, 'utf-8')
            + len(contents).to_bytes(4, 'little')
            + contents)
    s.send(data)
    receive_prefix = s.recv(len(send_prefix))
    print ('Received prefix ' + receive_prefix.decode('utf-8'))
    while True:
        try:
            msg_length_bytes = b0,b1,b2,b3 = s.recv(4)
#            msg_length = b3 + b2 * (1 << 8) + b1 * (1 << 16) + b0 * (1<<24)
            msg_length = b0 + b1 * (1 << 8) + b2 * (1 << 16) + b3 * (1<<24)
            msg_type = s.recv(1)
            print (f'Message length {msg_length} type {msg_type}')
            msg_bytes = s.recv(msg_length)
            msg = msg_bytes.decode('utf-8')
            print (f'Message: {msg}')
        except IOError:
            break
    s.close ()

host ="169.230.21.238"
port = 21213
json_data = {"model_name": "scene.glb",
             "position": {"x":0.0, "y":1.0, "z":0.0},
#             "rotation": {"x":0.0, "y":1.0, "z":0.0, "w":0.0},
             "scale": 2.0}
model_position_message_type = 1
connect_to_looksee(host, port, model_position_message_type, json_data)

print (f'Sent {json_data} to {host}:{port}')
