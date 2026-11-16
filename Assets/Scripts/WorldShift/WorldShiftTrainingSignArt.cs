using System.Collections.Generic;
using UnityEngine;

namespace Outlaws.WorldShift
{
    /// <summary>Shared readable pictograms for road signs and their paused enlarged previews.</summary>
    internal static class WorldShiftTrainingSignArt
    {
        private const int W=1200,H=360;
        private static readonly Dictionary<string,Texture2D> cache=new Dictionary<string,Texture2D>();
        private static readonly Color White=new Color(.97f,.98f,.94f), Ink=new Color(.04f,.07f,.09f);
        private static readonly Color Green=new Color(.02f,.27f,.19f);
        private static readonly Dictionary<char,string> letters=new Dictionary<char,string> {
            {'A',"01110/10001/10001/11111/10001/10001/10001"}, {'C',"01111/10000/10000/10000/10000/10000/01111"},
            {'D',"11110/10001/10001/10001/10001/10001/11110"}, {'E',"11111/10000/10000/11110/10000/10000/11111"},
            {'F',"11111/10000/10000/11110/10000/10000/10000"}, {'G',"01111/10000/10000/10111/10001/10001/01111"},
            {'H',"10001/10001/10001/11111/10001/10001/10001"}, {'I',"11111/00100/00100/00100/00100/00100/11111"},
            {'N',"10001/11001/11001/10101/10011/10011/10001"}, {'O',"01110/10001/10001/10001/10001/10001/01110"},
            {'P',"11110/10001/10001/11110/10000/10000/10000"}, {'R',"11110/10001/10001/11110/10100/10010/10001"},
            {'S',"01111/10000/10000/01110/00001/00001/11110"}, {'T',"11111/00100/00100/00100/00100/00100/00100"},
            {'V',"10001/10001/10001/10001/10001/01010/00100"}, {'W',"10001/10001/10001/10101/10101/11011/10001"},
            {'K',"10001/10010/10100/11000/10100/10010/10001"}, {'Y',"10001/10001/01010/00100/00100/00100/00100"},
            {'Z',"11111/00001/00010/00100/01000/10000/11111"}, {'=',"00000/11111/00000/11111/00000/00000/00000"},
            {':',"00000/00100/00100/00000/00100/00100/00000"}
        };
        public static string Description(LevelSignKind kind)
        {
            switch(kind)
            {
                case LevelSignKind.Steer: return "A / D OR ARROW KEYS\nSTEER LEFT / RIGHT";
                case LevelSignKind.Switch: return "SPACE\nSWITCH DAY / NIGHT";
                case LevelSignKind.Dashes: return "DASHED OUTLINES ARE SAFE\nSWITCHING MAKES THEM SOLID";
                case LevelSignKind.Oncoming: return "DODGE ONCOMING TRAFFIC\nA / D OR ARROW KEYS";
                case LevelSignKind.Freeze: return "DAY: MOVERS MOVE\nNIGHT: MOVERS FREEZE";
                default: return "SPACE IN MIDAIR\nSWITCH WORLDS TO LAND";
            }
        }
        public static Texture2D Load(string resource)
        {
            if(resource==null) return null;
            if(!resource.StartsWith("CurrentTraining")) return Resources.Load<Texture2D>(resource);
            if(cache.TryGetValue(resource,out var existing)) return existing;
            var p=new Color[W*H];
            for(int i=0;i<p.Length;i++) p[i]=Green;
            void Box(int x,int y,int w,int h,Color c) { for(int j=Mathf.Max(0,y);j<Mathf.Min(H,y+h);j++) for(int i=Mathf.Max(0,x);i<Mathf.Min(W,x+w);i++) p[(H-1-j)*W+i]=c; }
            void Line(int x,int y,int xx,int yy,int width,Color c) {
                int n=Mathf.Max(Mathf.Abs(xx-x),Mathf.Abs(yy-y));
                for(int i=0;i<=n;i++) {float t=n==0?0:(float)i/n;Box(Mathf.RoundToInt(Mathf.Lerp(x,xx,t))-width/2,Mathf.RoundToInt(Mathf.Lerp(y,yy,t))-width/2,width,width,c);}
            }
            void Disc(int x,int y,int r,Color c) {for(int j=-r;j<=r;j++) for(int i=-r;i<=r;i++) if(i*i+j*j<=r*r) Box(x+i,y+j,1,1,c);}
            void TextAt(string text,int x,int y,int scale,Color colour) {
                foreach(char ch in text) {
                    if(letters.TryGetValue(ch,out var glyph)) {var rows=glyph.Split('/');for(int j=0;j<7;j++) for(int i=0;i<5;i++) if(rows[j][i]=='1') Box(x+i*scale,y+j*scale,scale,scale,colour);}
                    x+=6*scale;
                }
            }
            void Text(string text,int y,int scale) => TextAt(text,(W-(text.Length*6-1)*scale)/2,y,scale,White);
            void Arrow(int x,int y,int direction) {
                Line(x-direction*40,y,x+direction*40,y,12,White);
                Line(x+direction*40,y,x+direction*16,y-24,12,White);
                Line(x+direction*40,y,x+direction*16,y+24,12,White);
            }
            void Key() {Box(460,155,280,85,White);Box(468,163,264,69,Ink);Text("SPACE",177,6);}
            void Sun(int x,int y,Color bg,bool night) {
                if(night) {Disc(x,y,25,Ink);Disc(x+15,y-7,23,bg);}
                else {Disc(x,y,17,Ink);for(int k=0;k<8;k++){float a=k*Mathf.PI/4;Line(x+(int)(Mathf.Cos(a)*24),y+(int)(Mathf.Sin(a)*24),x+(int)(Mathf.Cos(a)*33),y+(int)(Mathf.Sin(a)*33),6,Ink);}}
            }
            void Barrier(int x,int y,Color c,bool night) {
                Box(x,y,260,113,Ink);
                for(int k=0;k<3;k++){Box(x+5+k*86,y+5,78,103,c);Sun(x+44+k*86,y+54,c,night);}
            }
            void Plate(int x,int y,Color c) {
                Box(x,y,260,82,new Color(.34f,.37f,.39f));
                for(int k=0;k<260;k+=40){Box(x+k,y,28,9,c);Box(x+k,y+73,28,9,c);}
                for(int k=0;k<82;k+=30){Box(x,y+k,9,20,c);Box(x+251,y+k,9,20,c);}
                Line(x+85,y+12,x+85,y+68,3,Ink);Line(x+173,y+12,x+173,y+68,3,Ink);
            }
            void Spikes(int x,bool frozen) {
                Color teal=WorldShiftLevelProps.SideMoverColour;
                Box(x,215,245,38,teal);Disc(x+37,261,15,Ink);Disc(x+205,261,15,Ink);
                for(int k=0;k<5;k++){int cx=x+23+k*49;for(int j=0;j<65;j++) Box(cx-j/4,150+j,Mathf.Max(1,j/2),1,new Color(.55f,.61f,.65f));}
                Arrow(x+85,233,-1);Arrow(x+160,233,1);
                if(frozen) for(int k=0;k<7;k++){int cx=x+k*35;Line(cx,279,cx+10,240-(k%3)*9,18,new Color(.72f,.91f,1f));}
            }
            Box(8,8,W-16,5,White);Box(8,H-13,W-16,5,White);Box(8,8,5,H-16,White);Box(W-13,8,5,H-16,White);
            string kind=resource.Substring("CurrentTraining".Length);
            switch(kind)
            {
                case "Steer":
                    Text("STEER",35,7);
                    Box(470,140,260,137,Ink);Box(481,151,238,115,White);
                    for(int k=0;k<3;k++) Line(500+k*78,257,518+k*78,225,15,Ink);
                    // Both supported bindings are shown as keycaps, distinct from movement pictograms.
                    foreach(int x in new[]{90,295,775,980}) {Box(x,160,130,100,White);Box(x+9,169,112,82,Ink);}
                    TextAt("A",133,180,9,White);TextAt("D",818,180,9,White);
                    TextAt("OR",231,193,5,White);TextAt("OR",916,193,5,White);
                    Arrow(360,210,-1);Arrow(1045,210,1);
                    Text("A D OR ARROW KEYS",298,5);break;
                case "Switch":
                    Text("SWITCH",30,7);
                    Barrier(90,145,WorldShiftLevelProps.SwitchColour,false);Key();Plate(850,170,WorldShiftLevelProps.SwitchColour);
                    Arrow(405,200,1);Arrow(795,200,1);Text("DASHED = SAFE",298,5);break;
                case "Dashes":
                    Text("SWITCH",30,7);
                    Barrier(90,145,WorldShiftLevelProps.NightColour,true);Key();Plate(850,170,WorldShiftLevelProps.NightColour);
                    Arrow(405,200,1);Arrow(795,200,1);Text("DASHED = SAFE",298,5);break;
                case "Oncoming":
                    Text("DODGE",30,7);
                    Box(502,126,196,139,WorldShiftLevelProps.MoverColour);
                    Box(516,138,168,53,Ink);Box(516,207,46,25,White);Box(638,207,46,25,White);Box(565,205,70,51,Ink);
                    Box(489,230,23,48,Ink);Box(688,230,23,48,Ink);
                    Line(550,285,600,306,10,White);Line(600,306,650,285,10,White);
                    Arrow(335,210,-1);Arrow(865,210,1);break;
                case "Freeze":
                    Text("SPACE: FREEZE",30,7);Spikes(115,false);Key();Spikes(855,true);
                    Arrow(405,200,1);Arrow(795,200,1);break;
            }
            var texture=new Texture2D(W,H,TextureFormat.RGBA32,false){name=resource,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            texture.SetPixels(p);texture.Apply(false,true);cache[resource]=texture;return texture;
        }
    }
}
