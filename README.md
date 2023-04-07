# LookSee
Molecular viewer for Quest VR headsets

LookSee is a Unity application for Quest 2 and Quest Pro virtual reality headsets
to display 3-dimensional scenes of molecules and cells.  The scenes are created
in GLTF file format with the [UCSF ChimeraX](https://www.rbvi.ucsf.edu/chimerax/)
molecular visualization program.  Installing LookSee and using the Send to Quest
tool within UCSF ChimeraX are described
[here](https://www.rbvi.ucsf.edu/chimerax/data/looksee-mar2023/looksee.html).

LookSee uses the
[Oculus Integration](https://assetstore.unity.com/packages/tools/integration/oculus-integration-82022)
Unity package (version 49.0) for the VR camera (OVRCameraRig),
not included in this repository.  It also uses the
[GLTFast](https://github.com/atteneder/glTFast)
package (version 5.0.0).