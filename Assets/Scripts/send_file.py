#!/usr/bin/python3

def send_file_to_looksee(ip_address, port, path, send_prefix = 'LookSeeFile'):
    import socket
    s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    s.connect((host,port))

    from os.path import basename
    filename = basename(path)
    filename_bytes = bytes(filename, 'utf-8')

    f = open(path, 'rb')
    contents = f.read()
    f.close()

    data = (bytes(send_prefix, 'utf-8')
            + len(filename_bytes).to_bytes(4, 'little') + filename_bytes
            + len(contents).to_bytes(4, 'little') + contents)
    s.send(data)
    s.close ()

host ="169.230.21.238"
port = 21212
#path = '/Users/goddard/ucsf/LookSee/Assets/Scripts/send_file.py'
path = '/Users/goddard/Desktop/emdb_13795.glb'
send_file_to_looksee(host, port, path)

print (f'Sent {path} to {host}:{port}')
