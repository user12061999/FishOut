using UnityEngine;
namespace FishOut
{
    // The one framing calculation shared by the camera and the off-screen decor filter.
    public static class BoardView
    {
        public const float Pitch=50,Yaw=-10;
        public static Quaternion Rotation=>Quaternion.Euler(Pitch,Yaw,0);
        // Diagonal view of the whole shore and gate, kept clear of the top bar and the bottom buttons.
        public static void Compute(LevelData level,float aspect,out float size,out Vector2 middle)
        {
            const float top=.12f,bottom=.18f;
            float halfX=(level.width-1)*.5f+.95f,halfZ=(level.height-1)*.5f+1.3f,cut=2f;
            var gate=ConveyorNetwork.World(level,level.gate,0);
            var outline=new[]
            {
                new Vector3(-halfX+cut,0,-halfZ),new Vector3(halfX-cut,0,-halfZ),new Vector3(halfX,0,-halfZ+cut),new Vector3(halfX,0,halfZ-cut),
                new Vector3(halfX-cut,0,halfZ),new Vector3(-halfX+cut,0,halfZ),new Vector3(-halfX,0,halfZ-cut),new Vector3(-halfX,0,-halfZ+cut),
                gate+new Vector3(-2f,2.8f,.8f),gate+new Vector3(2f,2.8f,.8f)
            };
            var inverse=Quaternion.Inverse(Rotation);
            var low=new Vector2(float.MaxValue,float.MaxValue);var high=-low;
            foreach(var point in outline)
            {
                var view=inverse*point;
                low=Vector2.Min(low,view);high=Vector2.Max(high,view);
            }
            float span=1-top-bottom;
            size=Mathf.Max((high.y-low.y)/(2*span),(high.x-low.x)/(2*aspect));
            middle=(low+high)*.5f;middle.y-=(bottom+span*.5f-.5f)*2*size;
        }
    }
}
