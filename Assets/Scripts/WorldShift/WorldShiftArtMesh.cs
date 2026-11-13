using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Outlaws.WorldShift
{
    /// <summary>Flat-shaded, vertex-coloured meshes batched by coastal chunk; never adds colliders.</summary>
    internal sealed class WorldShiftArtMesh
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colours = new List<Color>();
        private readonly List<int> indices = new List<int>();
        public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color colour, bool twoSided = false)
        {
            int n = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colours.Add(colour); colours.Add(colour); colours.Add(colour);
            indices.Add(n); indices.Add(n + 1); indices.Add(n + 2);
            if (twoSided) Triangle(c, b, a, colour);
        }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour)
        { Triangle(a, b, c, colour); Triangle(a, c, d, colour); }
        public void Box(Vector3 p, Vector3 size, Color colour) => OrientedBox(p, size, Quaternion.identity, colour);
        private void OrientedBox(Vector3 p, Vector3 size, Quaternion rotation, Color c)
        {
            Vector3 P(float x, float y, float z) => p + rotation * Vector3.Scale(new Vector3(x, y, z), size * .5f);
            Quad(P(-1,1,-1), P(-1,1,1), P(1,1,1), P(1,1,-1), c);
            Quad(P(-1,-1,1), P(-1,-1,-1), P(1,-1,-1), P(1,-1,1), c);
            Quad(P(-1,-1,-1), P(-1,1,-1), P(1,1,-1), P(1,-1,-1), c);
            Quad(P(1,-1,1), P(1,1,1), P(-1,1,1), P(-1,-1,1), c);
            Quad(P(1,-1,-1), P(1,1,-1), P(1,1,1), P(1,-1,1), c);
            Quad(P(-1,-1,1), P(-1,1,1), P(-1,1,-1), P(-1,-1,-1), c);
        }
        /// <summary>Bevelled panel box with solid caps and readable highlights along all edges.</summary>
        public void ChamferBox(Vector3 centre, Vector3 size, float bevel, Color colour)
        {
            Vector3 h = size * .5f;
            float b = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * .7f);
            Vector3[] Ring(float inset, float z)
            {
                float x=h.x-inset,y=h.y-inset,c=Mathf.Max(.002f,b-inset*.5f);
                return new[] {new Vector3(-x+c,-y,z),new Vector3(x-c,-y,z),
                    new Vector3(x,-y+c,z),new Vector3(x,y-c,z),new Vector3(x-c,y,z),
                    new Vector3(-x+c,y,z),new Vector3(-x,y-c,z),new Vector3(-x,-y+c,z)};
            }
            var front=Ring(b,-h.z); var near=Ring(0,-h.z+b);
            var far=Ring(0,h.z-b); var back=Ring(b,h.z);
            for(int i=0;i<8;i++)
            {
                int j=(i+1)%8;
                Quad(centre+front[i],centre+front[j],centre+near[j],centre+near[i],colour);
                Quad(centre+near[i],centre+near[j],centre+far[j],centre+far[i],colour);
                Quad(centre+far[i],centre+far[j],centre+back[j],centre+back[i],colour);
                Triangle(centre+new Vector3(0,0,-h.z),centre+front[j],centre+front[i],colour);
                Triangle(centre+new Vector3(0,0,h.z),centre+back[i],centre+back[j],colour);
            }
        }

        public void Beam(Vector3 a, Vector3 b, float width, float depth, Color c)
        {
            if ((b - a).sqrMagnitude < .0001f) return;
            OrientedBox((a + b) * .5f, new Vector3(width, depth, (b-a).magnitude), Quaternion.LookRotation(b-a), c);
        }
        public void Cylinder(Vector3 p, float bottom, float top, float height, Color c, int sides)
        {
            for (int j = 0; j < sides; j++)
            {
                float a = j * Mathf.PI * 2f / sides, b = (j+1) * Mathf.PI * 2f / sides;
                Vector3 x = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), y = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
                Vector3 up = Vector3.up * height;
                Quad(p + y*bottom, p + y*top + up, p + x*top + up, p + x*bottom, c);
                Triangle(p+up, p+y*top+up, p+x*top+up, c);
            }
        }
        public void Rock(Vector3 p, Vector3 size, Color colour, int seed)
        {
            const int sides = 9;
            var bottom = new Vector3[sides]; var top = new Vector3[sides];
            for (int j = 0; j < sides; j++)
            {
                float a = j * Mathf.PI * 2f / sides;
                float variation = .86f + .14f * Mathf.Sin(j * 7.3f + seed * 2.1f);
                bottom[j] = p + new Vector3(Mathf.Cos(a)*size.x*variation, 0, Mathf.Sin(a)*size.z*variation);
                top[j] = p + new Vector3(Mathf.Cos(a)*size.x*.65f*variation, size.y*(.81f+.12f*Mathf.Sin(j+seed)), Mathf.Sin(a)*size.z*.65f*variation);
            }
            for (int j = 0; j < sides; j++)
            {
                int k = (j+1)%sides; Color c = colour * (1f - (j%3)*.06f); c.a=1;
                Quad(bottom[k], top[k], top[j], bottom[j], c);
                Triangle(top[j], p+Vector3.up*size.y*.9f, top[k], c);
            }
        }
        public void Wheel(Vector3 p, float radius, float width, Color c)
        {
            for (int i=0; i<20; i++)
            {
                float a=i*Mathf.PI/10f, b=(i+1)*Mathf.PI/10f;
                Vector3 x=new Vector3(0,Mathf.Cos(a)*radius,Mathf.Sin(a)*radius);
                Vector3 y=new Vector3(0,Mathf.Cos(b)*radius,Mathf.Sin(b)*radius);
                Vector3 side=Vector3.right*width*.5f;
                Quad(p+x-side,p+y-side,p+y+side,p+x+side,c);
                Triangle(p+side,p+x+side,p+y+side,c);
                Triangle(p-side,p+y-side,p+x-side,c);
            }
        }
        public void RopeRing(Vector3 centre, float radius, float thickness, Color c)
        {
            for(int i=0;i<20;i++)
            {
                float a=i*Mathf.PI/10f,b=(i+1)*Mathf.PI/10f;
                Beam(centre+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),
                    centre+new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius),thickness,thickness,c);
            }
        }
        public void Ring(float radius, float thickness, Color c)
        {
            for (int j = 0; j < 48; j++)
            {
                float a=j*Mathf.PI/24f, b=(j+1)*Mathf.PI/24f;
                Vector3 x=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)), y=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                Quad(x*(radius-thickness), y*(radius-thickness), y*radius, x*radius, c);
            }
        }
        public Mesh Mesh(string label)
        {
            var mesh = new Mesh { name = label, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetColors(colours); mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
    }
}
