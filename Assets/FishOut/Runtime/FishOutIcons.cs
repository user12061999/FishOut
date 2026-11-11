using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishOut
{
    // Interface icons are drawn from distance functions, so they need no imported art.
    public static class FishOutIcons
    {
        private const int Size=128;
        private static readonly Dictionary<string,Sprite> cache=new Dictionary<string,Sprite>();
        public static Sprite Gear=>Get("Gear",GearShape);
        public static Sprite Retry=>Get("Retry",RetryShape);
        public static Sprite Heart=>Get("Heart",HeartShape);
        public static Sprite Bulb=>Get("Bulb",BulbShape);
        public static Sprite Buoy=>Get("Buoy",BuoyShape);
        public static Sprite Star=>Get("Star",StarShape);
        public static Sprite Arrow=>Get("Arrow",ArrowShape);
        public static Sprite Twinkle=>Get("Twinkle",TwinkleShape);
        // A soft round glow (alpha falls off smoothly to the edge), for halos behind icons.
        public static Sprite Glow
        {
            get
            {
                if(cache.TryGetValue("Glow",out var sprite)&&sprite)return sprite;
                var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false){name="Icon Glow",wrapMode=TextureWrapMode.Clamp};
                var pixels=new Color32[Size*Size];
                for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
                {
                    float d=new Vector2((x+.5f)/Size*2-1,(y+.5f)/Size*2-1).magnitude;
                    float a=Mathf.Clamp01(1-d);pixels[y*Size+x]=new Color32(255,255,255,(byte)(a*a*255));
                }
                texture.SetPixels32(pixels);texture.Apply();
                return cache["Glow"]=Sprite.Create(texture,new Rect(0,0,Size,Size),new Vector2(.5f,.5f));
            }
        }
        private static Sprite Get(string name,Func<Vector2,float> shape)
        {
            if(cache.TryGetValue(name,out var sprite)&&sprite)return sprite;
            var texture=new Texture2D(Size,Size,TextureFormat.RGBA32,false){name="Icon "+name,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color32[Size*Size];const float pixel=2f/Size;
            for(int y=0;y<Size;y++)for(int x=0;x<Size;x++)
            {
                var p=new Vector2((x+.5f)/Size*2-1,(y+.5f)/Size*2-1);
                pixels[y*Size+x]=new Color32(255,255,255,(byte)(Mathf.Clamp01(.5f-shape(p)/pixel)*255));
            }
            texture.SetPixels32(pixels);texture.Apply();
            return cache[name]=Sprite.Create(texture,new Rect(0,0,Size,Size),new Vector2(.5f,.5f));
        }
        private static float GearShape(Vector2 p)
        {
            float angle=Mathf.Atan2(p.y,p.x),teeth=Mathf.Clamp(Mathf.Sin(angle*8)*3,-1,1);
            return Mathf.Max(p.magnitude-(.68f+.14f*teeth),.3f-p.magnitude);
        }
        private static float RetryShape(Vector2 p)
        {
            float ring=Mathf.Abs(p.magnitude-.56f)-.13f;
            // The ring opens on the upper right, where the arrowhead sits.
            float angle=Mathf.Atan2(p.y,p.x);
            float gap=angle>.15f&&angle<1f?.2f:-1;
            var q=p-new Vector2(.62f,.42f);
            float head=Mathf.Max(Mathf.Max(-q.y-.05f,q.y+Mathf.Abs(q.x)*1.25f-.36f),-1);
            return Mathf.Min(Mathf.Max(ring,gap),head);
        }
        private static float HeartShape(Vector2 p)
        {
            // The heart curve is implicit, so divide by its slope to get a usable edge distance.
            const float step=.01f;
            float here=HeartField(p);
            var slope=new Vector2(HeartField(p+Vector2.right*step)-here,HeartField(p+Vector2.up*step)-here)/step;
            return here/Mathf.Max(slope.magnitude,.001f);
        }
        private static float HeartField(Vector2 p)
        {
            var q=p*1.25f;float a=q.x*q.x+q.y*q.y-1;
            return a*a*a-q.x*q.x*q.y*q.y*q.y;
        }
        private static float BulbShape(Vector2 p)
        {
            float glass=(p-new Vector2(0,.22f)).magnitude-.56f;
            float neck=Mathf.Max(Mathf.Abs(p.x)-.25f,Mathf.Abs(p.y+.45f)-.3f);
            return Mathf.Min(glass,neck);
        }
        private static float BuoyShape(Vector2 p)=>Mathf.Abs(p.magnitude-.6f)-.27f;
        // A chunky arrow pointing straight down: shaft on top, head with its tip at the bottom.
        private static float ArrowShape(Vector2 p)
        {
            float shaft=Mathf.Max(Mathf.Abs(p.x)-.24f,Mathf.Abs(p.y-.38f)-.5f);
            float head=Mathf.Max(p.y+.02f,(Mathf.Abs(p.x)*.83f-.7f*(p.y+.9f))/1.086f);
            return Mathf.Min(shaft,head);
        }
        // Four-pointed twinkle (an astroid), the classic sparkle glint.
        private static float TwinkleShape(Vector2 p)=>(Mathf.Sqrt(Mathf.Abs(p.x))+Mathf.Sqrt(Mathf.Abs(p.y))-.95f)*.45f;
        private static float StarShape(Vector2 p)
        {
            // Fold into one arm of the star, then measure against its outer edge.
            float angle=Mathf.Repeat(Mathf.Atan2(p.x,p.y)+Mathf.PI/5,Mathf.PI*2/5)-Mathf.PI/5;
            var q=new Vector2(Mathf.Abs(Mathf.Sin(angle)),Mathf.Cos(angle))*p.magnitude;
            var tip=new Vector2(0,.92f);var inner=new Vector2(.24f,.33f);
            var edge=inner-tip;var normal=new Vector2(edge.y,-edge.x).normalized;
            return -Vector2.Dot(q-tip,normal);
        }
    }
}
