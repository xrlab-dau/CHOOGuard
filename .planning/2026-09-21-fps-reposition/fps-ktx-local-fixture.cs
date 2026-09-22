if(!UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Play required");
var p=UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Fps.FirstPersonResponder>();var c=p.GetComponent<UnityEngine.CharacterController>();
var origin=new UnityEngine.Vector3(88.9187956f,-.73000026f,76.4428219f);var right=new UnityEngine.Vector3(.93773824f,0,-.34734274f);var forward=new UnityEngine.Vector3(.34734274f,0,.93773824f);
System.Func<float,float,float,UnityEngine.Vector3> point=(x,y,z)=>origin+right*x+UnityEngine.Vector3.up*y+forward*z;
p.SetExternalInputMode(true);c.enabled=false;p.transform.position=point(-2.7f,.78f,-48.81f);c.enabled=true;UnityEngine.Physics.SyncTransforms();p.Resume(false);
for(int i=0;i<8;i++)p.StepInput(UnityEngine.Vector2.zero,new UnityEngine.Vector2(UnityEngine.Mathf.DeltaAngle(p.YawDegrees,110.3248713f),p.PitchDegrees),false,false,false,.05f);
var start=p.transform.position;p.Pause();return new{scope="Local coach2 boarding fixture positioned on source platform; no whole-map connectivity claim",position=new{x=start.x,y=start.y,z=start.z},yaw=p.YawDegrees,grounded=c.isGrounded};
