using System.IO;		// use MemoryStream
using System.Threading;		// use Thread
using System.Net.Sockets;	// use Socket
using System.Net;		// use IPAddress
using System;			// use Exception
using UnityEngine;		// use Debug
using UnityEngine.UI;		// use Text

public class FileReceiver : MonoBehaviour
{
    private ListenForFiles listen;
    private int port = 21212;
    private string prefix = "LookSeeFile";
    private string directory;
    public Text address_text;

    void Start()
    {
        this.directory = Application.persistentDataPath;
	byte[] prefix_bytes = System.Text.Encoding.UTF8.GetBytes(prefix);
        listen = new ListenForFiles(port, prefix_bytes, directory);
    }

    public void StartListening()
    {
	address_text.text = "Receive files at " + listen.GetLocalIPAddress();
	listen.StartListening();
    }	

    public void StopListening()
    {
	listen.StopListening();
    }	

    void OnApplicationQuit()
    {
        if (listen != null)
	  listen.StopListening();
    }
}

public class ListenForFiles
{
    private Thread listener_thread = null;
    private bool keep_listening = true;
    private Socket socket;
    private int port;
    private byte[] prefix;
    private string directory;
    
    public ListenForFiles(int port, byte[] prefix, string directory)
    {
	this.port = port;
	this.prefix = prefix;
	this.directory = directory;
    }

    public void StartListening()
    {
        keep_listening = true;
        listener_thread = new Thread(ListenWorker);
        listener_thread.Start();
    }
    
    public void StopListening()
    {
	keep_listening = false;
        socket.Close();
    }
    
    private void ListenWorker()
    {
        var receiveBuffer = new byte[0x10000]; // Read 64KB at a time

        // Set up a local socket for listening
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // Set up an endpoint and start listening
        var localEndpoint = new IPEndPoint(IPAddress.Any, port);
        socket.Bind(localEndpoint);
        socket.Listen(10);
        Debug.Log("Socket listening at IP address " + GetLocalIPAddress() + " port " + port);
// When asking to listen on all address (IPAddress.Any) local IP address reported as 0.0.0.0.
//        Debug.Log("Socket listening at IP address " + localEndpoint.Address.ToString() + " port " + port);
//        Debug.Log("Socket listening at IP address " + socket.LocalEndPoint.Serialize().ToString() + " port " + port);
//        Debug.Log("Socket listening at IP address " + IPAddress.Parse(((IPEndPoint)socket.LocalEndPoint).Address.ToString()) + " port " + port);

        while (keep_listening)
        {
            try
            {
                // This call will block until we get a message.
                // Using Async methods will have better performance, but this is simpler.
                var remoteSocket = socket.Accept();
                Debug.Log("Socket connection accepted.");
		
                // Connect to the remote client
                var receiveStream = new NetworkStream(remoteSocket);

                // Receive data
                MemoryStream data = new MemoryStream();
		bool check_prefix = true;
                while (true)
                {
                    var count = receiveStream.Read(receiveBuffer, 0, receiveBuffer.Length);
                    if (count == 0)
                      break;
                    data.Write(receiveBuffer, 0, count);
		    if (check_prefix && data.Length >= prefix.Length)
		    {
			check_prefix = false;
			byte[] data_prefix = data.ToArray()[0..prefix.Length];
			if (!EqualByteArrays(prefix, data_prefix))
			  break;
		    }
                }

                WriteStreamFile(data);
            }
            catch (Exception e)
            {
                // report errors and keep listening.
                Debug.Log("Network Error: " + e.Message);

                // Sleep 1 second so that we don't flood the output with errors
                Thread.Sleep(1000);
            }
        }
    }

    private bool EqualByteArrays(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
    {
	return a1.SequenceEqual(a2);
    }
    
    private bool WriteStreamFile(MemoryStream stream)
    {
       byte[] data = stream.ToArray();
       if (!EqualByteArrays(data[0..prefix.Length], prefix))
       {
         string stream_prefix = System.Text.Encoding.UTF8.GetString(data, 0, prefix.Length);
	 string eprefix = System.Text.Encoding.UTF8.GetString(prefix);
         Debug.Log("Prefix " + stream_prefix + " is not the expected " + eprefix);
	 return false;
       }

       int offset = prefix.Length;
       int filename_length = BitConverter.ToInt32(data[offset..(offset+4)], 0);
       if (filename_length <= 0)
       {
         Debug.Log("File name length is <= 0, got " + filename_length);
	 return false;
       }

       if (offset + 4 + filename_length > data.Length)
       {
         Debug.Log("File name length " + filename_length + " is to long for data length " + data.Length);
	 return false;
       }

       string filename = System.Text.Encoding.UTF8.GetString(data, offset+4, filename_length);
       Debug.Log("Got filename " + filename);

       offset += 4 + filename_length;
       int file_length = BitConverter.ToInt32(data[offset..(offset+4)], 0);
       if (file_length + offset+4 != data.Length)
       {
	 Debug.Log("File length " + file_length + " does not match data length " + (data.Length - (offset+4)));
	 return false;
       }

       string path = Path.Combine(directory, filename);
       var file = File.Open(path, FileMode.Create);
       file.Write(data, offset+4, file_length);
       file.Close();
       Debug.Log("Wrote " + file_length + " bytes to file " + path);

       return true;
    }

    public string GetLocalIPAddress()
    {
	var host = Dns.GetHostEntry(Dns.GetHostName());
	foreach (var ip in host.AddressList)
	{
	    if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        throw new Exception("No network adapters with an IPv4 address in the system!");
    }
}
 