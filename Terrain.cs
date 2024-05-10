using Godot;
using Godot.Collections;
using Godot.NativeInterop;
using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Security.Cryptography;

public partial class Terrain : MeshInstance3D
{
	[Export]
	public float Frequency { get; set; } = 0.7f;

	private FastNoiseLite noise;
	private MeshDataTool meshDataTool;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Generate();

		GD.Print("Terrain is ready!");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void Generate()
	{
		noise = new FastNoiseLite();
		meshDataTool = new MeshDataTool();

		var vertices = new Vector3[8] {
			new(-1, -1, -1),
			new( 1, -1, -1),
			new( 1, -1,  1),
			new(-1, -1,  1),
			new(-1,  1, -1),
			new( 1,  1, -1),
			new( 1,  1,  1),
			new(-1,  1,  1)
		};

		var indices = new int[36] {
			0, 1, 2, 2, 3, 0,
			1, 5, 6, 6, 2, 1,
			5, 4, 7, 7, 6, 5,
			4, 0, 3, 3, 7, 4,
			3, 2, 6, 6, 7, 3,
			4, 5, 1, 1, 0, 4
		};

		var normals = new Vector3[8] {
			new(-1, -1, -1),
			new( 1, -1, -1),
			new( 1, -1,  1),
			new(-1, -1,  1),
			new(-1,  1, -1),
			new( 1,  1, -1),
			new( 1,  1,  1),
			new(-1,  1,  1)
		};

		var uvs = new Vector2[8] {
			new(0, 0),
			new(1, 0),
			new(1, 1),
			new(0, 1),
			new(0, 0),
			new(1, 0),
			new(1, 1),
			new(0, 1)
		};

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);

		arrays[(int)Mesh.ArrayType.Vertex] = vertices;
		arrays[(int)Mesh.ArrayType.Index] = indices;
		arrays[(int)Mesh.ArrayType.Normal] = normals;
		arrays[(int)Mesh.ArrayType.TexUV	] = uvs;


		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

		noise.Frequency = Frequency;
		meshDataTool.CreateFromSurface(mesh, 0);

		for (int i = 0; i < meshDataTool.GetVertexCount(); i++)
		{
			var vertex = meshDataTool.GetVertex(i).Normalized();
			vertex *= noise.GetNoise3Dv(vertex) * 0.5f + 0.75f;
			meshDataTool.SetVertex(i, vertex);
		}

		for (int i = 0; i < meshDataTool.GetFaceCount(); i++)
		{
			var a = meshDataTool.GetFaceVertex(i, 0);
			var b = meshDataTool.GetFaceVertex(i, 1);
			var c = meshDataTool.GetFaceVertex(i, 2);

			var ap = meshDataTool.GetVertex(a);
			var bp = meshDataTool.GetVertex(b);
			var cp = meshDataTool.GetVertex(c);

			var normal = (bp - ap).Cross(cp - ap).Normalized();

			meshDataTool.SetVertexNormal(a, normal + meshDataTool.GetVertexNormal(a));
			meshDataTool.SetVertexNormal(b, normal + meshDataTool.GetVertexNormal(b));
			meshDataTool.SetVertexNormal(c, normal + meshDataTool.GetVertexNormal(c));
		}

		for (int i = 0; i < meshDataTool.GetVertexCount(); i++)
		{
			var v = meshDataTool.GetVertexNormal(i).Normalized();
			var color = new Color(v.X, v.Y, v.Z, 1.0f);
			meshDataTool.SetVertexNormal(i, v);
			meshDataTool.SetVertexColor(i, color);
			}

		mesh.ClearSurfaces();
		meshDataTool.CommitToSurface(mesh);
	}

	
}
