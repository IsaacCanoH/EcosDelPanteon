using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class PixelMenuSelectionGraphic : MaskableGraphic
{
    public enum Shape { Underline, Arch, Rectangle }
    public Shape shape;
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect r = rectTransform.rect;
        if (shape == Shape.Underline)
        {
            Quad(mesh,new Rect(r.xMin,r.center.y-4f,r.width,8f),color);
            Quad(mesh,new Rect(r.center.x-8f,r.center.y-8f,16f,16f),color);
            Quad(mesh,new Rect(r.center.x-3f,r.center.y-3f,6f,6f),new Color(1f,0.96f,0.62f,color.a));
            return;
        }
        Vector2[] path;
        if (shape == Shape.Rectangle)
            path = new[] { new Vector2(0.02f,0.12f),new Vector2(0.02f,0.88f),new Vector2(0.08f,0.98f),
                new Vector2(0.92f,0.98f),new Vector2(0.98f,0.88f),new Vector2(0.98f,0.12f),
                new Vector2(0.92f,0.02f),new Vector2(0.08f,0.02f),new Vector2(0.02f,0.12f) };
        else
            path = new[] { new Vector2(0.01f,0.01f),new Vector2(0.01f,0.73f),new Vector2(0.06f,0.81f),
                new Vector2(0.16f,0.88f),new Vector2(0.32f,0.94f),new Vector2(0.46f,0.97f),
                new Vector2(0.46f,1.00f),new Vector2(0.54f,1.00f),new Vector2(0.54f,0.97f),
                new Vector2(0.68f,0.94f),new Vector2(0.84f,0.88f),new Vector2(0.94f,0.81f),
                new Vector2(0.99f,0.73f),new Vector2(0.99f,0.01f),new Vector2(0.01f,0.01f) };
        for (int pass=0;pass<3;pass++)
        {
            float width = pass == 0 ? 24f : pass == 1 ? 16f : 8f;
            Color tint = color;
            tint.a *= pass == 0 ? 0.08f : pass == 1 ? 0.24f : 1f;
            for (int i=1;i<path.Length;i++)
            {
                Vector2 a = r.min+Vector2.Scale(path[i-1],r.size);
                Vector2 b = r.min+Vector2.Scale(path[i],r.size);
                int steps = Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x-a.x),Mathf.Abs(b.y-a.y))/8f));
                for (int j=0;j<=steps;j++)
                {
                    Vector2 p = Vector2.Lerp(a,b,(float)j/steps);
                    p = r.min+new Vector2(Mathf.Round((p.x-r.xMin)/8f)*8f,Mathf.Round((p.y-r.yMin)/8f)*8f);
                    Quad(mesh,new Rect(p.x-width*0.5f,p.y-width*0.5f,width,width),tint);
                }
            }
        }
    }
    private static void Quad(VertexHelper mesh, Rect r, Color tint)
    {
        int i = mesh.currentVertCount;
        mesh.AddVert(new Vector3(r.xMin,r.yMin),tint,Vector2.zero);
        mesh.AddVert(new Vector3(r.xMin,r.yMax),tint,Vector2.up);
        mesh.AddVert(new Vector3(r.xMax,r.yMax),tint,Vector2.one);
        mesh.AddVert(new Vector3(r.xMax,r.yMin),tint,Vector2.right);
        mesh.AddTriangle(i,i+1,i+2);
        mesh.AddTriangle(i,i+2,i+3);
    }
}
