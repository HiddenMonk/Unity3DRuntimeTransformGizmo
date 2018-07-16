# Unity3DRuntimeTransformGizmo
A runtime transform gizmo similar to unitys editor so you can translate (move, rotate, scale) objects at runtime.

Video demonstration - https://www.youtube.com/watch?v=IUQqhS8tsNo

WARNING - There is a bug in unity 5.4 and 5.5 that causes InverseTransformDirection to be affected by scale which will break negative scaling. Not tested, but updating to 5.4.2 should fix it - https://issuetracker.unity3d.com/issues/transformdirection-and-inversetransformdirection-operations-are-affected-by-scale

Just place the TransformGizmo on a gameobject with a camera.
Objects must have a collider on them to be selected.

Could use some work with how the moving is being handled. For example, I dont like how if you try to move something towards the camera, you cant get it to move past you to behind.

Added a pivot center mode so you can translate based on the Renderer.bounds.center instead of the normal pivot point.
The way its implemented is behind the scenes we create a temporary gameobject that is placed at the Renderer.bounds.center and becomes the parent of the selected object and we just translate that temporary gameobject. So if you see a temporary gameobject poping up and then disapearing it might be this temporary gameobject.
I dont like using temporary gameobjects since now if you happen to be transforming as well as somehow changing transform parents or destroying transforms, then when the runtime transform gizmo tries to put everything back to the way it was, it can mess up. So if you are using the center pivot mode, be sure to not be touching any of the transform layout (such as adding children and such) while doing so.
If you know of a way to scale a transform based on a different pivot let me know since then I think we can avoid using a temporary gameobject.
