using UnityEngine;
using UnityEngine.UI;

// Draws original chapter/button pixels inside their silhouette. Keeping scenery
// out of the corners lets one continuous background fill any screen proportion.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PixelMenuPlate : MaskableGraphic
{
    public enum Shape { Arch, Button }
    public Texture2D texture;
    public Rect sourceBounds;
    public Shape shape;
    public override Texture mainTexture { get { return texture!=null ? texture : Texture2D.whiteTexture; } }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Vector2[] path;
        if(shape==Shape.Button)
            path=new[]{new Vector2(0f,0.22f),new Vector2(0f,0.78f),new Vector2(0.08f,1f),
                new Vector2(0.92f,1f),new Vector2(1f,0.78f),new Vector2(1f,0.22f),new Vector2(0.92f,0f),new Vector2(0.08f,0f)};
        else
            path=new[]{new Vector2(0f,0f),new Vector2(0f,0.78f),new Vector2(0.08f,0.86f),
                new Vector2(0.20f,0.92f),new Vector2(0.38f,0.97f),new Vector2(0.38f,1f),
                new Vector2(0.62f,1f),new Vector2(0.62f,0.97f),new Vector2(0.80f,0.92f),
                new Vector2(0.92f,0.86f),new Vector2(1f,0.78f),new Vector2(1f,0f)};
        Vertex(mesh,new Vector2(0.5f,0.5f));
        foreach(var point in path) Vertex(mesh,point);
        for(int i=0;i<path.Length;i++) mesh.AddTriangle(0,i+1,(i+1)%path.Length+1);
    }
    private void Vertex(VertexHelper mesh,Vector2 unit)
    {
        Vector2 position=rectTransform.rect.min+Vector2.Scale(unit,rectTransform.rect.size);
        Vector2 uv=new Vector2((sourceBounds.x+unit.x*sourceBounds.width)/1672f,
            1f-(sourceBounds.y+(1f-unit.y)*sourceBounds.height)/941f);
        mesh.AddVert(position,color,uv);
    }
}
