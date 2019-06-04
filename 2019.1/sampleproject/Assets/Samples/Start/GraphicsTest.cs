using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GraphicsTest : MonoBehaviour
{
    public bool liveRender = false;
    public RenderTexture rt;
    Texture2D tex;
    Material m;

    // Start is called before the first frame update
    void Start()
    {
        //full screen 32bit rgba color, no depth
        rt = new RenderTexture( Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32 );
        rt.Create();

        tex = Resources.Load<Texture2D>("SomeTexture");
        //Shader.Find outputs the shader directly into the material
        // you could also just write a shader as text (a fun thing to try sometime, but also useful for code-generation!)
        //  I wouldn't be surprised if this is part of what happens when AAA-games are "compiling shaders"
        m = new Material(Shader.Find("Unlit/VertexColoredTexture"));
        m.SetTexture("_MainTex", tex);
    }

    void OnPostRender() {
        if ( liveRender ) {
            DrawStuff(null);
        }
    }

    [ContextMenu("Draw Stuff")]
    void DrawStuff(){
        DrawStuff(rt);
    }

    void DrawStuff( RenderTexture target )
    {
        if ( target != null )
            Graphics.SetRenderTarget(rt);

        //1: Render directly to RT with normalized ScreenUV Rect [0,1]
        //Graphics.BlitMultiTap( tex, rt, m, new Vector2(0.25f,0.25f), new Vector2(.75f,.75f) );

        //2: Render manually with matrixes and vertices
        //store current matrix
        GL.PushMatrix();

        //first pass of material is activated on gpu
        m.SetPass(0);
        //orthographic 0,0,-1 -> 1,1,100
        GL.LoadOrtho();

        //start rendering some triangles
        GL.Begin(GL.TRIANGLES);

        //set texcoord of following vertices
        GL.TexCoord2(0,1);
        GL.Color(Color.red);
        GL.Vertex3(0f, 1f, 0);  //add vertex
       
        GL.TexCoord2(1,1);
        GL.Color(Color.green);
        GL.Vertex3(1f, 1f, 0);
       
        GL.TexCoord2(0.5f,0);
        GL.Color(Color.blue);
        GL.Vertex3(0.5f, 0f, 0);

        GL.End();   //done with this batch of triangles

        //return to previous matrix
        GL.PopMatrix();

        //back to normal backbuffer rendering
        if ( target != null )
            Graphics.SetRenderTarget(null);
    }
}
