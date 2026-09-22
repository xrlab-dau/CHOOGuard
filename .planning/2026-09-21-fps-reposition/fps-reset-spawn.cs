if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play required");
var p=UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Fps.FirstPersonResponder>();var c=p.GetComponent<UnityEngine.CharacterController>();
var origin=new UnityEngine.Vector3(88.9187956f,-.73000026f,76.4428219f);var right=new UnityEngine.Vector3(.93773824f,0,-.34734274f);var forward=new UnityEngine.Vector3(.34734274f,0,.93773824f);
System.Func<float,float,float,UnityEngine.Vector3> point=(x,y,z)=>origin+right*x+UnityEngine.Vector3.up*y+forward*z;
p.SetExternalInputMode(true);c.enabled=false;p.transform.position=new UnityEngine.Vector3(-169.5003f,.145004f,-40.647f);c.enabled=true;UnityEngine.Physics.SyncTransforms();p.Pause();return new{scope="Reset spawn for corrected Metro visual inspection"};