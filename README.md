# LookSee
Molecular viewer for Quest VR headsets

LookSee releases and installation instructions are
[here](https://www.rbvi.ucsf.edu/chimerax/data/looksee-mar2023/looksee.html).

LookSee is a Unity application for Quest 3, Quest 2 and Quest Pro virtual reality headsets
to display 3-dimensional scenes of molecules and cells.  The scenes are created
in GLTF file format with the [UCSF ChimeraX](https://www.rbvi.ucsf.edu/chimerax/)
molecular visualization program, and sent to the headset with the ChimeraX
[Send to Quest](https://preview.cgl.ucsf.edu/chimerax/docs/user/tools/sendtoquest.html) tool.

LookSee uses Unity version 2023.1. the
[Oculus Integration](https://assetstore.unity.com/packages/tools/integration/oculus-integration-82022)
Unity package (version 57.0) for the VR camera (OVRCameraRig),
not included in this repository.  It is using Oculus XR version 4.1.
It also uses the [GLTFast](https://github.com/atteneder/glTFast)
package (version 5.2.0) to open GLTF files.