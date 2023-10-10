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
using System.IO;                        // use File
using System.Net.Sockets;                // use Socket
using System.Net;                        // use IPAddress
using System.Text;                        // use Encoding
using System.Threading.Tasks;                // use Task
using System;                                // use Exception
using UnityEngine;                        // use Debug
using UnityEngine.InputSystem;			// use InputAction
using UnityEngine.InputSystem.Utilities;	// use device.usages.Contains()
using TMPro;                            // use TextMeshProUGUI

public class Meeting : MonoBehaviour
{
    public LoadModels models;                     // Open models for adjusting positions.
    Dictionary<string, Position> model_positions; // Last sent model positions.
    Dictionary<string, bool> model_shown;	  // Last sent model show/hide state.
    public Transform left_wand, right_wand;       // For reporting positions to other participants.
    public Transform head;			  // For reporting head position to others.
    public ModelUI ui;				  // Use ui.settings

    private string looksee_version = "9";	  // TODO: Need to switch to v10 since version 8 and older used string compare
    private string minimum_compatible_version = "9";
    private int port = 21213;
    private string prefix = "LookSeeMeeting";
    private int max_participants = 100;
    private Socket listening_socket;                // Listen for new participants
    private List<Peer> peers = new List<Peer>();    // Connection to host or host to all participants
    private int frame = 0;
    private Dictionary<string, MeetingWands> wands = new Dictionary<string, MeetingWands>();	// Other participant's wands
    public GameObject face_prefab;			// Other participant's face
    private RoomCoordinates room_coords;
    private SetRoomCoordinates set_room_coords;		// Place two markers to define x-axis.
    public GameObject coord_marker1_prefab, coord_marker2_prefab;
    private Dictionary<string, Model> meeting_models;   // Map model identifier to Model

    void Start()
    {
        model_positions = new Dictionary<string, Position>();
        model_shown = new Dictionary<string, bool>();
	meeting_models = new Dictionary<string, Model>();
    }
    
    void Update()
    {
	if (peers.Count == 0)
	    return;
	    
        frame += 1;

        send_new_model_positions();
        send_model_shown_or_hidden();
        send_wand_positions();
	send_newly_opened_models();
	send_newly_closed_models();
	hide_inactive_wands();
    }

    public void start_hosting()
    {
        start_listening();
    }        

    public void stop_hosting()
    {
        stop_listening();
	leave_meeting();
    }        

    bool hosting()
    {
        return (listening_socket != null);
    }
    
    void OnApplicationQuit()
    {
        stop_hosting();
    }

    string device_id()
    {
      return SystemInfo.deviceUniqueIdentifier.Substring(0,8);
    }
    
    public void set_room_coordinates(bool enable)
    {
        if (enable)
	{
	  set_room_coords = new SetRoomCoordinates(left_wand, right_wand, models.gameObject.transform,
	  		    			   coord_marker1_prefab, coord_marker2_prefab);
          set_room_coords.show_current_coordinates(room_coords);
	}
        else if (set_room_coords != null)
	{
	  set_room_coords.finished();
	  set_room_coords = null;
	}
    }

    public void DropCoordinateMarker(InputAction.CallbackContext context)
    {
        if (set_room_coords == null)
	  return;

        if (!set_room_coords.drop_marker(context))
	  return;

	Vector3 x1 = Vector3.zero, x2 = Vector3.zero;
	if (!set_room_coords.marker_positions(ref x1, ref x2))
	  return;

        // Update room coordinates.
	if (room_coords == null)
	  room_coords = new RoomCoordinates();
	Matrix4x4 motion = room_coords.set_axis_markers(x1, x2);

	// Move wands so they reflect the new room coordinates.
	foreach (MeetingWands w in wands.Values)
	{
	  move_object(w.left_wand.transform, motion);
	  move_object(w.right_wand.transform, motion);
	}

	// Move models so they reflect the new room coordinates.
	foreach (Model m in models.open_models.models)
	  move_object(m.model_object.transform, motion);
    	record_current_positions();  // Avoid sending model moved messages

	// Save the new room coordinates in settings
    	ui.settings.save_meeting_coordinates(room_coordinate_system_identifier(), x1, x2);
        ui.settings.save();
    }

    bool load_room_coordinates()
    {
	Vector3 x1 = Vector3.zero, x2 = Vector3.zero;
	string room_id = room_coordinate_system_identifier();
	if (!ui.settings.find_meeting_coordinates(room_id, ref x1, ref x2))
	  return false;
	room_coords = new RoomCoordinates();
	room_coords.set_axis_markers(x1, x2);
	return true;
    }


    string room_coordinate_system_identifier()
    {
       // Looks like the Oculus API (Oculus Integration version 53.1) does not provide a way
       // to get a unique identifier for the physical room and its coordinate system
       // (probably depends on guardian boundary).  It has an OVRSpace class with a TryGetUuid()
       // method but I saw no way to get the current space.
       // So instead we identify the room and coordinate system by its size in millimeters.
       
       Vector3 dim = OVRManager.boundary.GetDimensions(OVRBoundary.BoundaryType.PlayArea);
       string room_id = (int)(dim.x * 1000) + "_" + (int)(dim.z * 1000);
       return room_id;
    }

    void move_object(Transform transform, Matrix4x4 motion)
    {
        Vector3 scale = Vector3.one;
	Matrix4x4 new_location = motion * Matrix4x4.TRS(transform.position, transform.rotation, scale);
	transform.position = new_location.GetPosition();
	transform.rotation = new_location.rotation;
    }
    
    public void start_listening()
    {
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

	load_room_coordinates();

	Invoke("accept_connections", 0f);
    }

    async public void join_meeting(string ip_address)
    {
        // This method only returns when the participant leaves the meeting
	// or if an error occurs while connecting.
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint host_address = new IPEndPoint(IPAddress.Parse(ip_address), port);
	try
	{
            await socket.ConnectAsync(host_address);
	}
    	catch (SocketException e)
	{
	  ui.report_join_failed("Connection to " + ip_address + " failed, socket error code " + e.ErrorCode);
	  return;
        }
	catch (Exception)
	{
	  ui.report_join_failed("Connection to " + ip_address + " failed");
	  return;
	}
	ui.report_join_success(ip_address);
	load_room_coordinates();
	Peer peer = new Peer(socket);
	peers.Add(peer);
	try
	{
          send_prefix(peer);
	  send_version(peer);
	  if (await check_for_compatible_version(peer))
	  {
	    send_all_open_models(peer);
            await process_messages(peer);
          }
	}
	finally
	{
	  leave_meeting();
	}
    }

    private void send_version(Peer peer)
    {
        VersionMessage message = new VersionMessage(looksee_version);
        send_message(message.serialize(), peer);
    }

    async private Task<bool> check_for_compatible_version(Peer peer)
    {
        if (!await verify_prefix(peer))
	  return false;

        byte [] version_message = await read_message(peer);
        if (version_message.Length == 0)
	  return false;
        if (version_message[0] != VersionMessage.message_type)
	  return false;
        VersionMessage m = VersionMessage.deserialize(version_message);

	if (version_less(m.version, minimum_compatible_version))
	{
	  if (hosting())
	    send_error_message("Joining this meeting requires LookSee version " + minimum_compatible_version +
	    		       " or higher.  You are using version " + m.version, peer);
          else
 	    ui.report_join_failed("The meeting host is using older LookSee version " + m.version +
	    			  " that is not compatible with your newer LookSee version " + looksee_version +
				  " that requires version " + minimum_compatible_version + " or newer.");
	  return false;
	}

        return true;
    }

    private bool version_less(string version1, string version2)
    {
        float v1 = float.Parse(version1), v2 = float.Parse(version2);
	return v1 < v2;
    }

    public void leave_meeting()
    {
        foreach(Peer peer in peers)
	    peer.close();
	peers.Clear();

	model_positions.Clear();
	model_shown.Clear();
	meeting_models.Clear();
	foreach (MeetingWands w in wands.Values)
	   w.remove_wand_depictions();
	wands.Clear();
        ui.left_meeting();	  
    }

    void remove_wands(string device_id)
    {
	if (device_id == null)
	  return;
        if (wands.ContainsKey(device_id))
	{
	  wands[device_id].remove_wand_depictions();
	  wands.Remove(device_id);
	  ui.show_error_message("Removed wands for " + device_id);
        }
    }
    
    async public Task<int> accept_connections()
    {
        // Accept a connection when one is made and process messages.
	// When a connected is accepted another task to accept more connections is started.

	Socket socket;
        try
        {
            socket = await listening_socket.AcceptAsync();
	}
        catch (Exception e)
	{
             debug_log("Error accepting socket connection: " + e.Message);
	     return 0;
	}

	Invoke("accept_connections", 0f);	// Accept more connections in a new task.

	Peer peer = new Peer(socket);
        send_prefix(peer);
        send_version(peer);

	if (!await check_for_compatible_version(peer))
	{
	    await peer.flush();
	    peer.close();
	    return 0;
	}

	if (peers.Count+1 >= max_participants)
	{
            debug_log("Too many participant tried to connect " + max_participants);
	    send_error_message("Meetings currently only allow " + max_participants + " persons.", peer);
	    await peer.flush();
	    peer.close();
	    return 0;
	}
	
	try
	{
	    peers.Add(peer);
	    send_all_open_models(peer);
	    return await process_messages(peer);
        }
        catch (Exception e)
        {
            debug_log("Error processing participant messages: " + e.Message);
        }
	finally
	{
	    peers.Remove(peer);
	    remove_wands(peer.device_id);
	    send_participant_left_message(peer.device_id);  // Tell other participants to remove this one's wands.
	    peer.close();
	}
	
        return 0;
    }

    async private Task<int> process_messages(Peer peer)
    {
        // Read and process messages from the remote client until the socket is closed.
        int msg_count = 0;
        while (true)
        {
            byte [] msg = await read_message(peer);
            if (msg.Length == 0)
              break;
            await process_message(msg, peer);
            msg_count += 1;
        }

	if (!hosting())
	    leave_meeting();

        return msg_count;
    }

    private void send_prefix(Peer peer)
    {
        byte[] prefix_bytes = System.Text.Encoding.UTF8.GetBytes(prefix);
        peer.send_bytes(prefix_bytes);
    }
    
    async private Task<bool> verify_prefix(Peer peer)
    {
        byte[] prefix_bytes = System.Text.Encoding.UTF8.GetBytes(prefix);
        if (prefix_bytes.Length == 0)
            return true;
        byte [] received_prefix = await read_bytes(peer, prefix_bytes.Length);
        bool same = equal_byte_arrays(prefix_bytes, received_prefix);
	return same;
    }

    private bool equal_byte_arrays(ReadOnlySpan<byte> a1, ReadOnlySpan<byte> a2)
    {
        return a1.Length == a2.Length && a1.SequenceEqual(a2);
    }

    async private Task<byte[]> read_message(Peer peer)
    {
        byte [] msg_size_bytes = await read_bytes(peer, 4);
        if (msg_size_bytes.Length != 4)
           return new byte[0];
        int msg_size = BitConverter.ToInt32(msg_size_bytes, 0);
        byte [] msg_bytes = await read_bytes(peer, msg_size);
        return msg_bytes;
    }

    async private Task<byte[]> read_bytes(Peer peer, int nbytes)
    {
        byte [] buffer = new byte[nbytes];
        int offset = 0;
	NetworkStream stream = peer.stream;
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

    async private Task<bool> process_message(byte[] msg, Peer peer)
    {
        byte message_type = msg[0];
        if (message_type == ModelPositionMessage.message_type)
            process_model_position_message(msg);
        else if (message_type == ModelShownMessage.message_type)
            process_model_shown_message(msg);
        else if (message_type == WandPositionMessage.message_type)
            process_wand_position_message(msg, peer);
        else if (message_type == OpenModelMessage.message_type)
            await process_open_model_message(msg);
        else if (message_type == CloseModelMessage.message_type)
            process_close_model_message(msg);
        else if (message_type == ParticipantLeftMessage.message_type)
            process_participant_left_message(msg);
        else if (message_type == ErrorMessage.message_type)
            process_error_message(msg);
	else
	    return false;
	if (hosting() && message_type != ErrorMessage.message_type)
	    relay_message(msg, peer);
	return true;
    }

    private void process_model_position_message(byte [] msg)
    {
        ModelPositionMessage m = ModelPositionMessage.deserialize(msg);
        if (!meeting_models.ContainsKey(m.model_id))
	{
            debug_log("Got model motion, but found no model id " + m.model_id);
            return;
        }
        Model model = meeting_models[m.model_id];
	if (room_coords != null)
	   room_coords.from_room(ref m.position, ref m.rotation);
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
        }
    }

    private void process_model_shown_message(byte [] msg)
    {
        ModelShownMessage m = ModelShownMessage.deserialize(msg);
        if (!meeting_models.ContainsKey(m.model_id))
	{
            debug_log("Got model shown, but found no model id " + m.model_id);
            return;
        }
        Model model = meeting_models[m.model_id];
	ui.show_or_hide_model(m.shown, model);
	ui.update_ui_controls();
    }

    private void process_wand_position_message(byte [] msg, Peer peer)
    {
        WandPositionMessage m = WandPositionMessage.deserialize(msg);
	if (room_coords != null)
	{
	   room_coords.from_room(ref m.left_position, ref m.left_rotation);
	   room_coords.from_room(ref m.right_position, ref m.right_rotation);
	   room_coords.from_room(ref m.head_position, ref m.head_rotation);
        }
	if (!wands.ContainsKey(m.device_id))
	    wands[m.device_id] = new MeetingWands(m.device_id, !ui.using_pass_through(), face_prefab);
	wands[m.device_id].set_wand_positions(m);
	peer.device_id = m.device_id;
    }

    private void hide_inactive_wands()
    {
	foreach (MeetingWands w in wands.Values)
	  w.show_if_active();
    }
    
    public void using_pass_through(bool pass)
    {
	foreach (MeetingWands w in wands.Values)
	  w.show_head(!pass);
    }
    
    async private Task<string> process_open_model_message(byte [] msg)
    {
        OpenModelMessage m = OpenModelMessage.deserialize(msg);
	Model model = await models.load_gltf_bytes(m.gltf_bytes, m.model_name);
	Transform transform = model.model_object.transform;
	transform.position = m.position;
	transform.rotation = m.rotation;
	transform.localScale = new Vector3(m.scale, m.scale, m.scale);
	meeting_models.Add(m.model_id, model);
        record_latest_position(m.model_id);
	models.open_models.add(model);
	return m.model_id;
    }

    private void process_close_model_message(byte [] msg)
    {
        CloseModelMessage m = CloseModelMessage.deserialize(msg);
	if (meeting_models.ContainsKey(m.model_id))
	{
	    Model model = meeting_models[m.model_id];
	    models.open_models.remove_model(model);
	    meeting_models.Remove(m.model_id);
	}
    }

    private void process_participant_left_message(byte [] msg)
    {
        ParticipantLeftMessage m = ParticipantLeftMessage.deserialize(msg);
	remove_wands(m.device_id);
    }
    
    public void stop_listening()
    {
        if (listening_socket == null)
            return;

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

    private void send_error_message(string text, Peer peer)
    {
        ErrorMessage message = new ErrorMessage(text);
        send_message(message.serialize(), peer);
    }

    private void process_error_message(byte [] msg)
    {
        ErrorMessage m = ErrorMessage.deserialize(msg);
	debug_log("Got error message " + m.error);
	ui.show_error_message(m.error);
    }

    private int send_new_model_positions()
    {
        int count = 0;
        foreach (var item in meeting_models)
        {
	   string model_id = item.Key;
           if (position_changed(model_id))
           {
               record_latest_position(model_id);
	       Model m = item.Value;
               ModelPositionMessage message = new ModelPositionMessage(model_id, m.model_object.transform);
	       if (room_coords != null)
	           room_coords.to_room(ref message.position, ref message.rotation);
               send_message_to_all(message.serialize());
               count += 1;
           }
        }
        
        return count;
    }

    private int send_model_shown_or_hidden()
    {
        int count = 0;
        foreach (var item in meeting_models)
        {
	   string model_id = item.Key;
           Model m = item.Value;
	   bool shown = m.model_object.activeSelf;
           if (!model_shown.ContainsKey(model_id) || model_shown[model_id] != shown)
           {
	       model_shown[model_id] = shown;
               ModelShownMessage message = new ModelShownMessage(model_id, shown);
               send_message_to_all(message.serialize());
               count += 1;
           }
        }
        
        return count;
    }

    private void send_message(byte [] msg, Peer peer)
    {
	byte[] msg_chunk = prepend_message_length(msg);
	peer.send_bytes(msg_chunk);
    }

    private void send_message_to_all(byte [] msg, bool allow_drop = false)
    {
	byte[] msg_chunk = prepend_message_length(msg);
	foreach (Peer peer in peers)
	{
	  bool drop = (allow_drop && peer.send_queue_length() >= 10);
	  if (!drop)
	    peer.send_bytes(msg_chunk);
	}
    }

    private void relay_message(byte[] msg, Peer from_peer)
    {
	byte[] msg_chunk = prepend_message_length(msg);
	bool allow_drop = (msg[0] == WandPositionMessage.message_type);
	foreach (Peer peer in peers)
	{
	  if (peer == from_peer)
	    continue;	// Don't send message to original sender.
	  bool drop = (allow_drop && peer.send_queue_length() >= 10);
	  if (!drop)
	    peer.send_bytes(msg_chunk);
	  /*
	    The socket send buffer is 512K bytes using Unity 2022.2.5f1 which
	    allows about 5000 wand position messages to get buffered up before
	    the WriteAsync() blocks and the send queue gets longer stopping
	    further wand positions messages.  Might be sensible to reduce the
	    5000 buffered messages by making the send buffer smaller.  Or I
	    could block sending if the peer has not sent us wand position in
	    the past 0.2 seconds, treating it as a ping signal.
	  */
	}
    }

    private byte[] message_bytes(byte message_type, string msg)
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
	return msg_chunk;
    }

    private byte[] prepend_message_length(byte[] msg)
    {
        // Concatenate chunk size and message.
        byte[] chunk_size_bytes = BitConverter.GetBytes(msg.Length);
        byte [] msg_chunk = new byte[chunk_size_bytes.Length + msg.Length];
        chunk_size_bytes.CopyTo(msg_chunk, 0);
        msg.CopyTo(msg_chunk, chunk_size_bytes.Length);
	return msg_chunk;
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

    private void send_wand_positions()
    {
        WandPositionMessage message = new WandPositionMessage(device_id(), left_wand, right_wand, head);
	if (room_coords != null)
	{
	    room_coords.to_room(ref message.left_position, ref message.left_rotation);
    	    room_coords.to_room(ref message.right_position, ref message.right_rotation);
       	    room_coords.to_room(ref message.head_position, ref message.head_rotation);
        }
        send_message_to_all(message.serialize(), true);
    }

    private int send_newly_opened_models()
    {
	int count = 0;
        foreach (Model m in models.open_models.models)
	{
	  if (!meeting_models.ContainsValue(m))
	  {
	      string model_name = m.model_object.name;
	      string model_id = new_model_id();
	      meeting_models.Add(model_id, m);
	      OpenModelMessage msg = new OpenModelMessage();
	      msg.model_id = model_id;
	      msg.model_name = model_name;
	      msg.gltf_bytes = File.ReadAllBytes(m.path);
	      Transform t = m.model_object.transform;
	      msg.position = t.position;
	      msg.rotation = t.rotation;
	      msg.scale = t.localScale.x;
	      send_message_to_all(msg.serialize());
	      count += 1;
	  }
        }
	return count;
    }

    private int send_all_open_models(Peer peer)
    {
	int count = 0;
        foreach (Model m in models.open_models.models)
	{
	  if (meeting_models.ContainsValue(m))
	  {
	    send_model(m, peer);
            count += 1;
	  }
        }
	return count;
    }

    private void send_model(Model m, Peer peer)
    {
	string model_name = m.model_object.name;
	string model_id = new_model_id();
	meeting_models.Add(model_id, m);
	OpenModelMessage msg = new OpenModelMessage();
	msg.model_id = model_id;
	msg.model_name = model_name;
	msg.gltf_bytes = File.ReadAllBytes(m.path);
	Transform t = m.model_object.transform;
	msg.position = t.position;
	msg.rotation = t.rotation;
	msg.scale = t.localScale.x;
	send_message(msg.serialize(), peer);
    }

    private string new_model_id()
    {
       byte[] key_bytes = new byte[6];
       System.Random r = new System.Random();
       r.NextBytes(key_bytes);
       string model_id = Convert.ToBase64String(key_bytes);
       return model_id;
    }
    
    private int send_newly_closed_models()
    {
	int count = 0;
	List<string> closed = new List<string>();
        foreach (var item in meeting_models)
	{
	  Model m = item.Value;
	  if (!models.open_models.models.Contains(m))
	  {
	      string model_id = item.Key;
	      closed.Add(model_id);  // Don't modify meeting_models while iterating over it.
	      CloseModelMessage msg = new CloseModelMessage();
	      msg.model_id = model_id;
	      send_message_to_all(msg.serialize());
      	      count += 1;
	  }
        }
	foreach(string model_id in closed)
	  meeting_models.Remove(model_id);

	return count;
    }

    private void send_participant_left_message(string device_id)
    {
	if (device_id == null)
	  return;
        ParticipantLeftMessage message = new ParticipantLeftMessage(device_id);
        send_message_to_all(message.serialize());
    }

    private void debug_log(string message)
    {
        GameObject debug = GameObject.Find("DebugText");
	if (debug != null)
          debug.GetComponentInChildren<TextMeshProUGUI>().text = message;
    }
}

[Serializable]
public class ModelPositionMessage
{
    public const int message_type = 1;
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

    public byte [] serialize()
    {
	return Messages.message_bytes(message_type, JsonUtility.ToJson(this));
    }

    static public ModelPositionMessage deserialize(byte [] msg)
    {
        string json = Encoding.UTF8.GetString(msg, 1, msg.Length-1);
        return JsonUtility.FromJson<ModelPositionMessage>(json);
    }
}

class Messages
{
    static public byte [] message_bytes(byte message_type, string json)
    {
        byte[] json_bytes = System.Text.Encoding.UTF8.GetBytes(json);
	byte [] msg = new byte[1 + json_bytes.Length];
	msg[0] = message_type;
	json_bytes.CopyTo(msg, 1);
	return msg;
    }
    static public byte [] message_bytes(byte message_type, string json, byte [] binary)
    {
        byte[] json_bytes = System.Text.Encoding.UTF8.GetBytes(json);
	byte [] json_length = BitConverter.GetBytes((Int32)json_bytes.Length);
	int length = 1 + 4 + json_bytes.Length + binary.Length;
	byte [] msg = new byte[length];
	msg[0] = message_type;
	json_length.CopyTo(msg, 1);
	json_bytes.CopyTo(msg, 1 + 4);
	binary.CopyTo(msg, 1 + 4 + json_bytes.Length);
	return msg;
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

[Serializable]
public class ModelShownMessage
{
    public const int message_type = 7;
    public string model_id;
    public bool shown;

    public ModelShownMessage(string model_id, bool shown)
    {
      this.model_id = model_id;
      this.shown = shown;
    }

    public byte [] serialize()
    {
	return Messages.message_bytes(message_type, JsonUtility.ToJson(this));
    }

    static public ModelShownMessage deserialize(byte [] msg)
    {
        string json = Encoding.UTF8.GetString(msg, 1, msg.Length-1);
        return JsonUtility.FromJson<ModelShownMessage>(json);
    }
}

// Cylinders depicting other participant's wands.
class MeetingWands
{
    public string device_id;
    public GameObject left_wand, right_wand, head;
    private Vector3 wand_scale = new Vector3(0.02f, 0.2f, 0.02f);
    private Vector3 head_scale = new Vector3(0.2f, 0.2f, 0.03f);
    private GameObject face_prefab;
    private float last_update_time = 0f, inactive_hide_time = 3.0f;	// seconds
    
    public MeetingWands(string device_id, bool show_head, GameObject face_prefab)
    {
	this.device_id = device_id;
        left_wand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        left_wand.transform.localScale = wand_scale;
        right_wand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        right_wand.transform.localScale = wand_scale;
	this.face_prefab = face_prefab;
	this.show_head(show_head);
    }

    public void show_head(bool show)
    {
      if (show && head == null)
        head = UnityEngine.Object.Instantiate(face_prefab);
      else if (!show && head != null)
      {
        UnityEngine.Object.Destroy(head);
        head = null;
      }
    }
    
    public void remove_wand_depictions()
    {
	UnityEngine.Object.Destroy(left_wand);
	left_wand = null;
	UnityEngine.Object.Destroy(right_wand);
	right_wand = null;
	show_head(false);
    }
    
    public void set_wand_positions(WandPositionMessage msg)
    {
        Transform ltf = left_wand.transform, rtf = right_wand.transform;
        ltf.position = msg.left_position;
        ltf.rotation = msg.left_rotation;
        rtf.position = msg.right_position;
        rtf.rotation = msg.right_rotation;
	if (head != null)
	{
	  Transform htf = head.transform;
	  htf.position = msg.head_position;
  	  htf.rotation = msg.head_rotation;
	}
	last_update_time = Time.realtimeSinceStartup;
	show_if_active();
    }

    public bool show_if_active()
    {
        bool show = (Time.realtimeSinceStartup < last_update_time + inactive_hide_time);
	if (show != left_wand.activeSelf)
	{
  	  left_wand.SetActive(show);
	  right_wand.SetActive(show);
	  if (head != null)
	    head.SetActive(show);
	}
	return show;
    }
    
}

[Serializable]
public class WandPositionMessage
{
    public const int message_type = 2;
    public string device_id;
    public Vector3 left_position, right_position, head_position;
    public Quaternion left_rotation, right_rotation, head_rotation;

    public WandPositionMessage(string device_id, Transform left, Transform right, Transform head)
    {
	this.device_id = device_id;
        left_position = left.position;
        left_rotation = left.rotation;
        right_position = right.position;
        right_rotation = right.rotation;
	head_position = head.position;
	head_rotation = head.rotation;
    }

    public byte [] serialize()
    {
	return Messages.message_bytes(message_type, JsonUtility.ToJson(this));
    }

    static public WandPositionMessage deserialize(byte [] msg)
    {
        string json = Encoding.UTF8.GetString(msg, 1, msg.Length-1);
        return JsonUtility.FromJson<WandPositionMessage>(json);
    }
}

class RoomCoordinates
{
    public Vector3 x1, x2;
    public Matrix4x4 local_to_room = Matrix4x4.identity;
    public Matrix4x4 room_to_local = Matrix4x4.identity;
    
    public void to_room(ref Vector3 position, ref Quaternion rotation)
    {
        Vector3 scale = Vector3.one;
	Matrix4x4 new_location = local_to_room * Matrix4x4.TRS(position, rotation, scale);
	position = new_location.GetPosition();
	rotation = new_location.rotation;
    }

    public void from_room(ref Vector3 position, ref Quaternion rotation)
    {
        Vector3 scale = Vector3.one;
	Matrix4x4 new_location = room_to_local * Matrix4x4.TRS(position, rotation, scale);
	position = new_location.GetPosition();
	rotation = new_location.rotation;
    }

    public Matrix4x4 set_axis_markers(Vector3 x1, Vector3 x2)
    {
        this.x1 = x1;
	this.x2 = x2;
	Vector3 xaxis = x2 - x1;
	xaxis.y = 0f;
	Quaternion rotation = Quaternion.FromToRotation(Vector3.right, xaxis);
	Vector3 origin = 0.5f * (x1 + x2);
        Vector3 scale = Vector3.one;
	Matrix4x4 new_room_to_local = Matrix4x4.TRS(origin, rotation, scale);
	Matrix4x4 motion = new_room_to_local * local_to_room;
	room_to_local = new_room_to_local;
	local_to_room = new_room_to_local.inverse;
	return motion;
    }
}

class SetRoomCoordinates
{
    Transform left_wand, right_wand;
    Transform marker_parent;
    GameObject coord_marker1_prefab, coord_marker2_prefab;
    GameObject wand_marker1, wand_marker2;
    GameObject placed_marker1, placed_marker2;
    
    public SetRoomCoordinates(Transform left_wand, Transform right_wand, Transform marker_parent,
    	   	              GameObject coord_marker1_prefab, GameObject coord_marker2_prefab)
    {
      this.left_wand = left_wand;
      this.right_wand = right_wand;
      this.marker_parent = marker_parent;
      this.coord_marker1_prefab = coord_marker1_prefab;
      this.coord_marker2_prefab = coord_marker2_prefab;

      Vector3 position1 = right_wand.TransformPoint(new Vector3(0,1.1f,0));
      wand_marker1 = UnityEngine.Object.Instantiate(coord_marker1_prefab, position1, Quaternion.identity);
      wand_marker1.transform.SetParent(right_wand.parent);

      Vector3 position2 = left_wand.TransformPoint(new Vector3(0,1.1f,0));
      wand_marker2 = UnityEngine.Object.Instantiate(coord_marker2_prefab, position2, Quaternion.identity);
      wand_marker2.transform.SetParent(left_wand.parent);
    }
    
    public void show_current_coordinates(RoomCoordinates room_coords)
    {
      place_marker(1, room_coords.x1);
      place_marker(2, room_coords.x2);
    }

    public void finished()
    {
      GameObject.Destroy(wand_marker1);
      GameObject.Destroy(wand_marker2);
      if (placed_marker1 != null)
        GameObject.Destroy(placed_marker1);
      if (placed_marker2 != null)
        GameObject.Destroy(placed_marker2);
    }

    public bool drop_marker(InputAction.CallbackContext context)
    {
      if (!context.performed)
        return false;
      if (context.control.device.usages.Contains(UnityEngine.InputSystem.CommonUsages.RightHand))
        place_marker(1, wand_marker1.transform.position);
      else
        place_marker(2, wand_marker2.transform.position);
      return true;
    }

    void place_marker(int n, Vector3 position)
    {
      if (n == 1)
      {
        if (placed_marker1 == null)
	  placed_marker1 = UnityEngine.Object.Instantiate(coord_marker1_prefab, position, Quaternion.identity, marker_parent);
	else
	  placed_marker1.transform.position = position;
      }
      else if (n == 2)
      {
        if (placed_marker2 == null)
	  placed_marker2 = UnityEngine.Object.Instantiate(coord_marker2_prefab, position, Quaternion.identity, marker_parent);
	else
	  placed_marker2.transform.position = position;
      }
    }

    public bool marker_positions(ref Vector3 x1, ref Vector3 x2)
    {
      if (placed_marker1 == null || placed_marker2 == null)
        return false;
      x1 = placed_marker1.transform.position;
      x2 = placed_marker2.transform.position;
      return true;
    }
}

[Serializable]
public class OpenModelMessage
{
    public const int message_type = 3;
    public string model_id;	// Unique identifier
    public string model_name;	// File name
    public Vector3 position;
    public Quaternion rotation;
    public float scale;

    [field: System.NonSerialized]
    public byte [] gltf_bytes;

    public byte [] serialize()
    {
	return Messages.message_bytes(message_type, JsonUtility.ToJson(this), gltf_bytes);
    }

    static public OpenModelMessage deserialize(byte [] msg)
    {
        // Message has type, JSON length, JSON header, GLTF bytes.
        int json_size = BitConverter.ToInt32(msg, 1);
        string json = Encoding.UTF8.GetString(msg, 1 + 4, json_size);
        OpenModelMessage m = JsonUtility.FromJson<OpenModelMessage>(json);
	int gltf_offset = 1 + 4 + json_size;
	int gltf_size = msg.Length - gltf_offset;
	byte [] gltf = new byte [gltf_size];
	System.Array.Copy(msg, gltf_offset, gltf, 0, gltf_size);
	m.gltf_bytes = gltf;
	return m;
    }
}

[Serializable]
public class CloseModelMessage
{
    public const int message_type = 4;
    public string model_id;

    public byte [] serialize()
    {
	return Messages.message_bytes(message_type, JsonUtility.ToJson(this));
    }

    static public CloseModelMessage deserialize(byte [] msg)
    {
        string json = Encoding.UTF8.GetString(msg, 1, msg.Length-1);
        return JsonUtility.FromJson<CloseModelMessage>(json);
    }
}

[Serializable]
public class VersionMessage
{
    public const int message_type = 5;
    public string version;

    public VersionMessage(string version)
    {
      this.version = version;
    }
    
    public byte [] serialize()
    {
	return Messages.message_bytes(message_type, JsonUtility.ToJson(this));
    }

    static public VersionMessage deserialize(byte [] msg)
    {
        string json = Encoding.UTF8.GetString(msg, 1, msg.Length-1);
        return JsonUtility.FromJson<VersionMessage>(json);
    }
}

[Serializable]
public class ErrorMessage
{
    public const int message_type = 6;
    public string error;

    public ErrorMessage(string text)
    {
	error = text;
    }
    
    public byte [] serialize()
    {
	return Messages.message_bytes(message_type, JsonUtility.ToJson(this));
    }

    static public ErrorMessage deserialize(byte [] msg)
    {
        string json = Encoding.UTF8.GetString(msg, 1, msg.Length-1);
        return JsonUtility.FromJson<ErrorMessage>(json);
    }
}

[Serializable]
public class ParticipantLeftMessage
{
    public const int message_type = 8;
    public string device_id;

    public ParticipantLeftMessage(string device_id)
    {
	this.device_id = device_id;
    }
    
    public byte [] serialize()
    {
	return Messages.message_bytes(message_type, JsonUtility.ToJson(this));
    }

    static public ParticipantLeftMessage deserialize(byte [] msg)
    {
        string json = Encoding.UTF8.GetString(msg, 1, msg.Length-1);
        return JsonUtility.FromJson<ParticipantLeftMessage>(json);
    }
}

// Connection to another meeting participant.
public class Peer
{
    public NetworkStream stream;
    public string device_id;
    private Queue<byte[]> send_message_queue = new Queue<byte[]>();
    private bool have_send_task = false;	// Have at most 1 task send messages.

    public Peer(Socket socket)
    {
      stream = new NetworkStream(socket, true);
    }

    public void close()
    {
      stream.Close();
    }

    public void send_bytes(byte[] msg)
    {
	send_message_queue.Enqueue(msg);
	if (!have_send_task)
	  _ = send_from_queue();	// Start async without waiting.
    }

    async private Task<int> send_from_queue()
    {
      have_send_task = true;
      int count = 0;
      try
      {
        while (send_message_queue.Count > 0)
        {
	  count += send_message_queue.Count;
          byte[] messages = concatenated_queue_messages();
          await stream.WriteAsync(messages, 0, messages.Length);
/*
  6/27/23: I tried using a synchronous Write() for each message with
    stream.WriteTimeout = 1 (msec), but the timeout was never
    invoked and once the socket buffer (512k) filled it blocked
    the Update loop and froze the application.  So then I switched
    to using only WriteAsync() and concatenated all the queued messages
    because Unity was only running this task once per update and so
    messages would get backlogged with 3 person meetings where there
    is more than 1 message being sent per frame.
*/  
        }
      }
      finally
      {
        have_send_task = false;
      }
      return count;
    }

    async public Task<int> flush()
    {
      int count = 0;
      while (send_message_queue.Count > 0)
      {
        byte[] msg = send_message_queue.Dequeue();
        await stream.WriteAsync(msg, 0, msg.Length);
	count += 1;
      }
      return count;
    }
    
    public int send_queue_length()
    {
      return send_message_queue.Count;
    }

    public byte[] concatenated_queue_messages()
    {
      int length = 0;
      foreach(byte[] msg in send_message_queue)
        length += msg.Length;
      byte[] concat = new byte[length];
      int offset = 0;
      while (send_message_queue.Count > 0)
      {
          byte[] msg = send_message_queue.Dequeue();
	  msg.CopyTo(concat, offset);
	  offset += msg.Length;
      }
      return concat;
    }
}