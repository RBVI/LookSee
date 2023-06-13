//
// Allow two Quest headsets to view the same GLTF scene in the same physical room
// with pass-through video.
//
// Message passing for synchronization.
// They send messages to each other about the position and size of models and
// positions of hand-controller wands and copy scene files so people see models
// and wands in the same positions within the physical room.
//
// Third-party inter-process messaging libraries.
// I looked at messaging libraries (e.g. MSMQ, gRPC, SignalIR, ...) but it seems
// like the complexity of those is not worth the trouble, and a simple TCP socket
// implementation is probably the most maintainable and debugable.
// The ChimeraX meeting command uses this approach, and JSON messages with a
// leading byte count for each message.  I think that is a reasonable first try.
//
// Support 2 users initially.
// Common coordinate system.
// The simplest case is two users in the same room.  With 2 users there can be a
// single socket between them.  With 3 or more we probably want a hub that relays
// all messages.  Also having the 2 in the same room eliminates the need for computer
// audio.  But being in the same room with pass-through video adds the complication
// that the headsets need top use the same coordinate system so the models appear to be
// in the same place in the room.  While Meta has some spatial anchor capability it
// looks like a nuissance to use with Meta's cloud synchronization.  So I think for
// a first try I should have each user click on the same two points in the room to
// define the coordinate system.  We can assume vertical is the same, and just need
// an origin and z-rotation (4 parameters).  Positions encoded in messages will be
// in this common coordinate system.
//
// Async/await socket handling.
// Unity is not thread safe so it is probably simplest to handle the socket communication
// using async/await which keeps all operations in the same thread so that positions can
// be directly read or set by the messaging code.  Here is some basic async/await socket
// client and server code:
//
//    https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/sockets/socket-services
//
// Handling more than 2 participants.
// I should give some thought about how to handle > 2 people in the same room.  Probably
// the meeting host can act as the hub with each other headset connected to it.  Then
// some additional relaying of messages is required by the hub.  This is how the ChimeraX
// meeting command handles more than 2 participants.
//
// Initial user interface.
// For an initial version I envision one headset clicks a "Start meeting" checkbutton
// and that reports an IP address, and the other headset clicks a "Join meeting" checkbutton
// and is prompted to enter the IP address.  That will need some VR keyboard or numeric pad
// support.  Probably I can get a Unity asset for that input.  The IP address can be saved
// in a file for future sessions in case the headsets routinely use the same IP addresses.
//
// Don't need IP address user interface immediately.
// For a first test I can forgo VR entry of the IP address by just putting the correct IP
// address in the file that saves it.
//
// Handling headset sleep.
// I'm not sure what happens to the socket connection when the user removes the Quest
// headset.  The LookSee app might sleep and then we will want to avoid continuously
// streaming updated hand-controller positions to the sleeping headset since those
// messages will be buffered up and block the sending application.  So maybe the
// message sender should only use one async function instance.  If it is blocked
// then sending messages is paused until it unblocks.  A simple integer counter
// can indicate how many sends are in progress.  Maybe it is also necessary to
// have only one send active otherwise they could interleave the messages.  I guess
// the sender should hold its own queue of messages and so the messages are kept in order.
//
// Naming models.
// When multiple models are opened, how will the messages refer to them uniquely to
// specify their positions?  It would be simplest to have each one use a unique file name.
// But currently I allow you to get open multiple copies of the same file.  Also if
// multiple participants try to open files that have the same names, how do I prevent
// the transmitted files from overwriting the local user's file?
//
// Use cached GLTF files?
// I want the transmitted files to not have to be transmitted again.  So I'd like a
// hash value computed for each file and an index of hash values each headset can
// check if it already has the files.  Maybe each open file is assigned a random
// 10 character alphanumeric string.  No simple solution will handle the situation
// where two users open a file at the same time possibly with the same identifier,
// filename, hash.  The random name is almost assured to produce a unique name.
// The name shown in the user interface can be the file name for the original copy.
// But a transmitted copy may have another name to avoid overwriting an earlier file.
//
// Don't use locally cached GLTF files.
// It is much simpler to transmit an open file every time.  The files are will typically
// be smaller than 25 Mbytes since larger gltf files won't render fast enough.  So 200 Mbits
// and on a local wifi network that should take only a second.  The drawback of trying to
// reuse a locally cached file is that back and forth messages are needed.  The opener would
// have to say try to open this file, then the receiver would need to say, I don't have that
// file send it to me, then the sender sends it.  In the mean time the opener might have
// sent messages to move that model, so now the receiver will have to remember the messages
// for the not yet open file.  This seems like much more complexity than it is worth.
// The sent files can still be cached on the local headset so they can be viewed in a
// future session.
//
// Duplicate ChimeraX VR meeting protocol?
// Are there advantages to using exactly the message protocol that ChimeraX is using?
// This could allow ChimeraX VR to interoperate with Quest VR as long as ChimeraX does
// not change the scene.  It probably won't be useful in practice because the ChimeraX
// level of detail will be too high for the Quest to render.  But the message passing
// requirements and all the same or similar so it looks like my plan is to implement
// something very similar anyways.  So if it becomes desirable to allow ChimeraX to
// participate in a meetings with standalone Quest it should be possible to adapt the
// code to it.
//
// Rendevous server.
// Could Quest use the ChimeraX rendevous server to allow meetings between users not in
// the same room?  I think it is possible, that server just allows naming ssh tunnels.
// Maybe the Quest would need an ssh library to make a tunnel (does ChimeraX do this with
// a subprocess running the ssh executable?).  But the main issue is that my current
// meeting rendevous is just one server that is set to allow only 10 simultaneous meetings
// so bandwidth is adequate.  If the Quest meetings are popular this capacity will not
// be adequate.
//
// Treat wand models like other models.
// Probably it is simplest to treat other participants wands as just another GLTF model.
// Will need to flag them as a model that cannot be moved and should not appear in GUI.
// But they can have randomly assigned name like other models, and receive the same type
// of messages for position updates.  If headset models for participants not in the same
// room are added they can also use gltf models that are transmitted like all other models.
//
// NetworkStream vs Socket.
// I initially used NetworkStream to wrap the sockets since it provides await/async methods
// while Socket uses an older different async approach.  But in testing it appeared that
// reading a message with two ReadAsync() calls only happened over two graphics frames.
// So even with just one message per frame the receiver falls behind in processing messages.
// One idea is to use non-async routines if received data is already buffered and only use
// the async version if not enough is buffered.  Unfortunately NetworkStream does not provide
// a way to report how many bytes are buffered.  (Actually it does document a property Length
// that does exactly that, but it is also documented to be never supported!).  Sockets can
// report the number of buffered bytes.  Here is a web page about using Socket with await/async
//    https://www.codeproject.com/Articles/5274512/How-to-Implement-and-Use-Awaitable-Sockets-in-Csha
// It looks like NetworkStream can handle my case.  It has a DataAvailable bool property and if
// true it says read will return immediately (I guess with partial data).  So I can just to
// non-async reads while DataAvailable is true, and switch to async read if no data is available.
// Somehow I think I am missing the problem.  I would think await ReadAsync() would not
// release control to another task because ReadAsync would return a Task that can immediately
// produce some data.  Those details aren't documented.  Using this approach made message updates
// not fall behind in a simple single model (no wands) test case.
//
// Coordinate system synchronization.
// It might be best if only one person needs to know how align coordinate systems.  Here's a possible
// approach.  The person who does the alignment sees that the computer generated wand cylinders don't
// line up with the video of the controllers for the other person.  So they drag that person's wands
// so they do line up with the video of the controllers.  The drag only allows translation and
// rotation about the vertical axis to avoid wrong tilts of the wands.  The new alignment is just
// remembered by the host who did the alignment and is applied to the incoming message positions from
// that participant and outgoing positions to that participant.  Initially both participants will see
// the other's hand controllers misaligned.  But as one person aligns them, both will see each others
// come into alignment.  It is probably best that only one person do the alignment since if two try
// it they would just fight with each other.  So might only allow the meeting host to do it.
//
// Pass-through video framerate is slow.
// The pass-through video on the Quest Pro seems to update about 5 frames per second.
// The stuttery update is the same in the LookSee app or if just turning on pass-through
// in the Quest home room.  Seems usable but is a serious drawback of the pass-through,
// in addition to the low resolution, poor dynamic range and colors.
//
using System.Collections.Generic;        // use Dictionary
using System.IO;                        // use Path
using System.Net.Sockets;                // use Socket
using System.Net;                        // use IPAddress
using System.Text;                        // use Encoding
using System.Threading.Tasks;                // use Task
using System;                                // use Exception
using UnityEngine;                        // use Debug
using UnityEngine.InputSystem;			// use InputAction
using UnityEngine.InputSystem.Utilities;	// use device.usages.Contains()
using UnityEngine.UI;                        // use Text
using TMPro;                            // use TextMeshProUGUI

public class Meeting : MonoBehaviour
{
    public LoadModels models;                     // Open models for adjusting positions.
    Dictionary<string, Position> model_positions; // Last sent model positions.
    public Text host_address_text;                // For reporting meeting IP address.
    public Text join_address_text;                // For reporting meeting IP address.
    public Transform left_wand, right_wand;       // For reporting positions to other participants.
    public ModelUI ui;				  // Use ui.settings

    private int port = 21213;
    private string prefix = "LookSeeMeeting";
    private Socket listening_socket;                // Listen for new participants
    private bool keep_listening = true;
    private NetworkStream peer;                 // Connection to other meeting participant.
    private int frame = 0;
    private const byte model_position_message_type = 1;
    private const byte wand_position_message_type = 2;
    private const byte open_model_message_type = 3;
    private const byte close_model_message_type = 4;
    private MeetingWands wands;
    private AlignCoordinates align;
    private AlignWandsByHand align_by_hand;
    private int blocked_message_send_count = 0;
    private Dictionary<string, Model> meeting_models;   // Map model identifier to Model

    void Start()
    {
        model_positions = new Dictionary<string, Position>();
	meeting_models = new Dictionary<string, Model>();
	join_address_text.text = "Join " + ui.settings.meeting_last_join_ip_address;
    }
    
    async void Update()
    {
	if (peer == null)
	    return;
	    
        frame += 1;
	adjust_wand_alignment();
	// TODO: Count how many WriteAsync tasks have not completed and if it gets high (100?)
	// then stop trying to send more until the number goes back down (0?).
	// Otherwise we get thousands of blocked writes when another participant takes off
	// headset and it sleeps.
        await send_new_model_positions();
        await send_wand_positions();
	await send_newly_opened_models();
	await send_newly_closed_models();
    }

    public void start_hosting()
    {
        host_address_text.text = "Meeting at " + get_local_ip_address();
        start_listening();
	use_previous_alignment();
        GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Started listening for meeting";
    }        

    public void stop_hosting()
    {
        stop_listening();
	leave_meeting();
    }        

    void OnApplicationQuit()
    {
        stop_hosting();
    }

    private void use_previous_alignment()
    {
	if (align == null && !ui.settings.meeting_alignment.isIdentity)
	{
	  align = new AlignCoordinates();
	  align.remote_to_local = ui.settings.meeting_alignment;
        }
    }
    
    public void enable_hand_alignment(bool enable)
    {
        if (enable)
	  align_by_hand = new AlignWandsByHand();
        else
	{
	  align_by_hand = null;
	  ui.settings.save();
        }
    }

    public void StartWandAlignment(InputAction.CallbackContext context)
    {
        if (align_by_hand != null)
	  align_by_hand.start_wand_alignment(context, left_wand, right_wand);
    }

    bool adjust_wand_alignment()
    {
	if (align_by_hand == null || wands == null)
	    return false;

        if (align == null)
	    align = new AlignCoordinates();
	Matrix4x4 motion = new Matrix4x4();
	if (align_by_hand.adjust_alignment(align, ref motion))
	{
	    move_object(wands.left_wand.transform, motion);
	    move_object(wands.right_wand.transform, motion);
	    foreach (Model m in models.open_models.models)
	        move_object(m.model_object.transform, motion);
	    record_current_positions();  // Avoid sending model moved message
	    ui.settings.meeting_alignment = align.remote_to_local;  // Save alignment in settings
	    return true;
        }
	return false;
    }

    void move_object(Transform transform, Matrix4x4 motion)
    {
        Vector3 scale = Vector3.one;
	Matrix4x4 new_location = motion * Matrix4x4.TRS(transform.position, transform.rotation, scale);
	transform.position = new_location.GetPosition();
	transform.rotation = new_location.rotation;
    }
    
    async public void start_listening()
    {
        keep_listening = true;

        // Set up a local socket for listening
        listening_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // Set up an endpoint and start listening
        var localEndpoint = new IPEndPoint(IPAddress.Any, port);
        listening_socket.Bind(localEndpoint);
        listening_socket.Listen(10);

        Debug.Log("Socket listening at IP address " + get_local_ip_address() + " port " + port);
// When asking to listen on all address (IPAddress.Any) local IP address reported as 0.0.0.0.
//        Debug.Log("Socket listening at IP address " + localEndpoint.Address.ToString() + " port " + port);
//        Debug.Log("Socket listening at IP address " + listening_socket.LocalEndPoint.Serialize().ToString() + " port " + port);
//        Debug.Log("Socket listening at IP address " + IPAddress.Parse(((IPEndPoint) listening_socket.LocalEndPoint).Address.ToString()) + " port " + port);

        while (keep_listening)
            await handle_connection();
    }

    async public void join_meeting(string ip_address)
    {
        GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Joining meeting at IP address " + ip_address;
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint host_address = new IPEndPoint(IPAddress.Parse(ip_address), port);
        await socket.ConnectAsync(host_address);
            GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Connected";
        peer = new NetworkStream(socket, true);
        await send_prefix();
//        GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Sent prefix";
	await send_newly_opened_models();
        await process_messages();
    }

    public void leave_meeting()
    {
        if (peer == null)
	    return;

        peer.Close();
        peer = null;
	model_positions.Clear();
	meeting_models.Clear();
	blocked_message_send_count = 0;
	if (wands != null)
	{
	   wands.remove_wand_depictions();
	   wands = null;
        }
    }
    
    async public Task<int> handle_connection()
    {
        // This only handles one connection at a time and returns when the connection is closed.
        
        try
        {
            Socket socket = await listening_socket.AcceptAsync();
        GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text += "\r\nSocket connection accepted";
            Debug.Log("Socket connection accepted.");

            peer = new NetworkStream(socket, true);
            await send_prefix();
            return await process_messages();
        }
        catch (Exception e)
        {
            // report errors and keep listening.
            Debug.Log("Network Error: " + e.Message);
        }
        return 0;
    }

    async private Task<int> process_messages()
    {
        // Read and process messages from the remote client until the socket is closed.
        int msg_count = 0;
        if (await verify_prefix(peer))
        {
        GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "\r\nGot prefix " + prefix + " and processing messages";
            // Receive data
            while (true)
            {
                byte [] msg = await read_message(peer);
                if (msg.Length == 0)
                  break;
                await process_message(msg);
                msg_count += 1;
//		if (msg_count % 200 == 0)
//                GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Processed message " + msg_count;
            }
        }
	leave_meeting();
        return msg_count;
    }

    async private Task send_prefix()
    {
        byte[] prefix_bytes = System.Text.Encoding.UTF8.GetBytes(prefix);
        await peer.WriteAsync(prefix_bytes, 0, prefix_bytes.Length);
    }
    
    async private Task<bool> verify_prefix(NetworkStream stream)
    {
        byte[] prefix_bytes = System.Text.Encoding.UTF8.GetBytes(prefix);
        if (prefix_bytes.Length == 0)
            return true;
        byte [] received_prefix = await read_bytes(stream, prefix_bytes.Length);
        return equal_byte_arrays(prefix_bytes, received_prefix);
    }

    private bool equal_byte_arrays(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
    {
        return a1.Length == a2.Length && a1.SequenceEqual(a2);
    }

    async private Task<byte[]> read_message(NetworkStream stream)
    {
        byte [] msg_size_bytes = await read_bytes(stream, 4);
        if (msg_size_bytes.Length != 4)
           return new byte[0];
        int msg_size = BitConverter.ToInt32(msg_size_bytes, 0);
        byte [] msg_bytes = await read_bytes(stream, msg_size);
        return msg_bytes;
    }

    async private Task<byte[]> read_bytes(NetworkStream stream, int nbytes)
    {
        byte [] buffer = new byte[nbytes];
        int offset = 0;
        while (offset < nbytes)
        {
            int count;
            // Do a synchronous read if data available.  Otherwise in Unity 2022.2.5f1
            // only one ReadAsync() is done per graphics frame even when all data is available
            // and that creates huge lag processing messages.
            if (stream.DataAvailable)
                    count = stream.Read(buffer, offset, nbytes-offset);
            else
                count = await stream.ReadAsync(buffer, offset, nbytes-offset);
            if (count == 0)
                return buffer[0..offset];
            offset += count;
        }
        return buffer;
    }

    async private Task<bool> process_message(byte[] msg)
    {
        // GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text += "\r\nGot message type" + msg[0];
        string json = Encoding.UTF8.GetString(msg, 1, msg.Length-1);
        byte message_type = msg[0];
        if (message_type == model_position_message_type)
            process_model_position_message(json);
        else if (message_type == wand_position_message_type)
            process_wand_position_message(json);
        else if (message_type == open_model_message_type)
            await process_open_model_message(json);
        else if (message_type == close_model_message_type)
            process_close_model_message(json);
	else
	    return false;
	return true;
    }

    private void process_model_position_message(string json)
    {
        ModelPositionMessage m = JsonUtility.FromJson<ModelPositionMessage>(json);
        if (!meeting_models.ContainsKey(m.model_id))
	{
            GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text += "Got model motion, but found no model id " + m.model_id + ", name " + m.model_id;
            return;
        }
        Model model = meeting_models[m.model_id];
	if (align != null)
	   align.to_local(ref m.position, ref m.rotation);
        Transform t = model.model_object.transform;
        bool position_changed = false;
        if (m.position != Vector3.zero)
        {
            t.position = m.position;
            position_changed = true;
        }
        if (m.rotation.x != 0 || m.rotation.y != 0 || m.rotation.z != 0 || m.rotation.w != 0)
        {
            t.rotation = m.rotation;
            position_changed = true;
        }
        if (m.scale != 0)
        {
            t.localScale = new Vector3(m.scale, m.scale, m.scale);
            position_changed = true;
        }
        if (position_changed)
	{
            record_latest_position(m.model_id);
	    GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Updated model position " + m.position + " at frame " + frame;
        }
    }

    private void process_wand_position_message(string json)
    {
        WandPositionMessage m = JsonUtility.FromJson<WandPositionMessage>(json);
	if (align != null)
	{
	   align.to_local(ref m.left_position, ref m.left_rotation);
	   align.to_local(ref m.right_position, ref m.right_rotation);
        }
	if (wands == null)
	    wands = new MeetingWands();
	wands.set_wand_positions(m);
    }

    async private Task<string> process_open_model_message(string json)
    {
        GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Got open model message " + json.Length;
        OpenModelMessage m = JsonUtility.FromJson<OpenModelMessage>(json);
	Model model = await models.load_gltf_bytes(m.gltf_data(), m.model_name);
	Transform transform = model.model_object.transform;
	transform.position = m.position;
	transform.rotation = m.rotation;
	transform.localScale = new Vector3(m.scale, m.scale, m.scale);
	meeting_models.Add(m.model_id, model);
        record_latest_position(m.model_id);
	models.open_models.add(model);
	return m.model_id;
    }

    private void process_close_model_message(string json)
    {
        GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Got close model message ";
        CloseModelMessage m = JsonUtility.FromJson<CloseModelMessage>(json);
	if (meeting_models.ContainsKey(m.model_id))
	{
	    Model model = meeting_models[m.model_id];
	    models.open_models.remove_model(model);
	    meeting_models.Remove(m.model_id);
	}
    }
    
    public void stop_listening()
    {
        if (listening_socket == null)
            return;

        keep_listening = false;
        listening_socket.Close();
        listening_socket = null;
    }

    public string get_local_ip_address()
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

    async private Task<bool> send_new_model_positions()
    {
        if (peer == null)
            return false;

//	if (waiting_to_send_message())
//	    return false;

        bool sent = false;
        foreach (var item in meeting_models)
        {
	   string model_id = item.Key;
           if (position_changed(model_id))
           {
	   	if (waiting_to_send_message())
		{
		    GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Could not send model motion, write is blocked " + blocked_message_send_count;
		    continue;
		}
		else
		{
		    GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Sending model motion " + frame;
		}
               record_latest_position(model_id);
	       Model m = item.Value;
               ModelPositionMessage message = new ModelPositionMessage(model_id, m.model_object.transform);
	       if (align != null)
	           align.from_local(ref message.position, ref message.rotation);
               string msg = JsonUtility.ToJson(message);
               await send_message(model_position_message_type, msg);
               sent = true;
           }
        }
        
        return sent;
    }

    async private Task<int> send_message(byte message_type, string msg)
    {
        byte[] msg_bytes = System.Text.Encoding.UTF8.GetBytes(msg);
        int chunk_size = msg_bytes.Length + 1;  // Add one byte for message type
        byte[] chunk_size_bytes = BitConverter.GetBytes(chunk_size);

        // Concatenate chunk size, message type, and message.
        byte [] msg_chunk = new byte[chunk_size_bytes.Length + chunk_size];
        chunk_size_bytes.CopyTo(msg_chunk, 0);
        int message_type_offset = chunk_size_bytes.Length;
        msg_chunk[message_type_offset] = message_type;
        int message_offset = chunk_size_bytes.Length+1;
        msg_bytes.CopyTo(msg_chunk, message_offset);

        blocked_message_send_count += 1;
	try
	{
	    await peer.WriteAsync(msg_chunk, 0, msg_chunk.Length);
        }
	finally
	{
            blocked_message_send_count -= 1;
	}

        return msg_chunk.Length;
    }

    private bool waiting_to_send_message()
    {
        return blocked_message_send_count > 0;
    }
	
    private bool position_changed(string model_id)
    {
	if (!meeting_models.ContainsKey(model_id))
	    return false;

        if (!model_positions.ContainsKey(model_id))
	    return true;
	Model model = meeting_models[model_id];
	bool changed = model_positions[model_id].moved(model.model_object.transform);
	return changed;
    }

    private void record_latest_position(string model_id)
    {
       if (!meeting_models.ContainsKey(model_id))
           return;
       Model model = meeting_models[model_id];
       if (!model_positions.ContainsKey(model_id))
           model_positions.Add(model_id, new Position(model.model_object.transform));
       else
           model_positions[model_id].update_position(model.model_object.transform);
    }

    private void record_current_positions()
    {
        foreach (string model_id in meeting_models.Keys)
          record_latest_position(model_id);
    }

    async private Task<bool> send_wand_positions()
    {
        if (peer == null)
            return false;

	if (waiting_to_send_message())
	    return false;

        WandPositionMessage message = new WandPositionMessage(left_wand, right_wand);
	if (align != null)
	{
	    align.from_local(ref message.left_position, ref message.left_rotation);
    	    align.from_local(ref message.right_position, ref message.right_rotation);
        }
        string msg = JsonUtility.ToJson(message);
        await send_message(wand_position_message_type, msg);
        return true;
    }

    async private Task<int> send_newly_opened_models()
    {
	int count = 0;
        foreach (Model m in models.open_models.models)
	{
	  if (!meeting_models.ContainsValue(m))
	  {
	      string model_name = m.model_object.name;
              GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Sending open model message " + model_name;
	      string model_id = new_model_id();
	      meeting_models.Add(model_id, m);
	      OpenModelMessage msg = new OpenModelMessage();
	      msg.model_id = model_id;
	      msg.model_name = model_name;
	      byte[] gltf_data = File.ReadAllBytes(m.path);
	      msg.set_gltf_data(gltf_data);
	      Transform t = m.model_object.transform;
	      msg.position = t.position;
	      msg.rotation = t.rotation;
	      msg.scale = t.localScale.x;
	      string message = JsonUtility.ToJson(msg);
	      await send_message(open_model_message_type, message);
	      count += 1;
              GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text += "\r\nSent open model message " + model_name;
	  }
        }
	return count;
    }

    private string new_model_id()
    {
       byte[] key_bytes = new byte[6];
       System.Random r = new System.Random();
       r.NextBytes(key_bytes);
       string model_id = Convert.ToBase64String(key_bytes);
       return model_id;
    }
    
    async private Task<int> send_newly_closed_models()
    {
	int count = 0;
        foreach (var item in meeting_models)
	{
	  Model m = item.Value;
	  if (!models.open_models.models.Contains(m))
	  {
              // GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Sending close model message " + model_id;
	      string model_id = item.Key;
	      meeting_models.Remove(model_id);
	      CloseModelMessage msg = new CloseModelMessage();
	      msg.model_id = model_id;
	      string message = JsonUtility.ToJson(msg);
	      await send_message(close_model_message_type, message);
      	      count += 1;
              // GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text += "\r\nSent close model message " + model_id;
	  }
        }
	return count;
    }
}

[Serializable]
public class ModelPositionMessage
{
    public string model_id;
    public Vector3 position;
    public Quaternion rotation;
    public float scale;

    public ModelPositionMessage(string model_id, Transform transform)
    {
        this.model_id = model_id;
        this.position = transform.position;
        this.rotation = transform.rotation;
        this.scale = transform.localScale.x;
    }
}

class Position
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;

    public Position(Transform t)
    {
        update_position(t);
    }

    public void update_position(Transform t)
    {
        position = t.position;
        rotation = t.rotation;
        scale = t.localScale;
    }

    public bool moved(Transform t)
    {
        bool same = (t.position == position && t.rotation == rotation && t.localScale == scale);
        return !same;
    }
}

// Cylinders depicting other participant's wands.
class MeetingWands
{
    public GameObject left_wand, right_wand;
    private Vector3 wand_scale = new Vector3(0.02f, 0.2f, 0.02f);
    
    public MeetingWands()
    {
        left_wand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        left_wand.transform.localScale = wand_scale;
        right_wand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        right_wand.transform.localScale = wand_scale;
    }

    public void remove_wand_depictions()
    {
	UnityEngine.Object.Destroy(left_wand);
	left_wand = null;
	UnityEngine.Object.Destroy(right_wand);
	right_wand = null;
    }
    
    public WandPositionMessage wand_positions()
    {
        return new WandPositionMessage(left_wand.transform, right_wand.transform);
    }
    
    public void set_wand_positions(WandPositionMessage msg)
    {
        Transform ltf = left_wand.transform, rtf = right_wand.transform;
        ltf.position = msg.left_position;
        ltf.rotation = msg.left_rotation;
        rtf.position = msg.right_position;
        rtf.rotation = msg.right_rotation;
    }
}

[Serializable]
public class WandPositionMessage
{
    public Vector3 left_position, right_position;
    public Quaternion left_rotation, right_rotation;

    public WandPositionMessage(Transform left, Transform right)
    {
        left_position = left.position;
        left_rotation = left.rotation;
        right_position = right.position;
        right_rotation = right.rotation;
    }
}

class AlignCoordinates
{
    public Matrix4x4 remote_to_local = Matrix4x4.identity;

    public AlignCoordinates copy()
    {
        AlignCoordinates acopy = new AlignCoordinates();
	acopy.remote_to_local = remote_to_local;
	return acopy;
    }
    
    public void to_local(ref Vector3 position, ref Quaternion rotation)
    {
        Vector3 scale = Vector3.one;
	Matrix4x4 new_location = remote_to_local * Matrix4x4.TRS(position, rotation, scale);
	position = new_location.GetPosition();
	rotation = new_location.rotation;
    }

    public void from_local(ref Vector3 position, ref Quaternion rotation)
    {
        Vector3 scale = Vector3.one;
	Matrix4x4 new_location = remote_to_local.inverse * Matrix4x4.TRS(position, rotation, scale);
	position = new_location.GetPosition();
	rotation = new_location.rotation;
    }

    public void update_alignment(Matrix4x4 motion)
    {
        remote_to_local = motion * remote_to_local;
    }
}

class AlignWandsByHand
{
    Transform wand;
    bool button_pressed = false;
    Matrix4x4 last_wand_pose;
    bool have_last_pose = false;
    
    public void start_wand_alignment(InputAction.CallbackContext context,
    	   			     Transform left_wand, Transform right_wand)
    {
    // In testing I could only get button release with InputAction Action Type of "pass through".
    /*
      if (context.started)
      	    GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Align start";
      if (context.canceled)
//      	    GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Align cancel " + context.ReadValueAsButton() ;
      	    GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Align cancel " + context.action.IsPressed() ;

      if (!context.performed)
        return;
    */
    
      bool left = context.control.device.usages.Contains(UnityEngine.InputSystem.CommonUsages.LeftHand);
      wand = (left ? left_wand : right_wand);
      bool prev_pressed = button_pressed;
//      button_pressed = (context.action.ReadValue<float>() > 0);  // For analog button, e.g. grip, trigger
      button_pressed = context.action.IsPressed();
      GameObject.Find("DebugText").GetComponentInChildren<TextMeshProUGUI>().text = "Align pressed B " + button_pressed;
      if (button_pressed != prev_pressed)
          have_last_pose = false;
    }

    public bool adjust_alignment(AlignCoordinates align, ref Matrix4x4 motion)
    {
      if (!button_pressed)
          return false;
	  
      Vector3 scale = Vector3.one;
      Matrix4x4 pose = Matrix4x4.TRS(wand.position, wand.rotation, scale);
      if (have_last_pose)
      {
          motion = pose * last_wand_pose.inverse;
	  motion = only_z_rotation(motion, wand.position);
	  last_wand_pose = pose;
	  align.update_alignment(motion);
	  return true;
      }
      else
      {
          last_wand_pose = pose;
	  have_last_pose = true;
	  return false;
      }
    }
    
    Matrix4x4 only_z_rotation(Matrix4x4 m, Vector3 center)
    {
	Vector3 p = m.GetPosition();
	Quaternion r = m.rotation;
	float angle;
	Vector3 axis;
	r.ToAngleAxis(out angle, out axis);
	Quaternion ry = Quaternion.AngleAxis(angle*axis.y, Vector3.up);
	Vector3 py = p + r*center - ry*center;
	Vector3 scale = Vector3.one;
	return Matrix4x4.TRS(py, ry, scale);
    }
}

[Serializable]
public class OpenModelMessage
{
    public string model_id;	// Unique identifier
    public string model_name;	// File name
    public string gltf_base64;
    public Vector3 position;
    public Quaternion rotation;
    public float scale;

    public byte[] gltf_data()
    {
    	return Convert.FromBase64String(gltf_base64);
    }
    
    public void set_gltf_data(byte[] gltf)
    {
        gltf_base64 = Convert.ToBase64String(gltf);
    }
}

[Serializable]
public class CloseModelMessage
{
    public string model_id;
}
