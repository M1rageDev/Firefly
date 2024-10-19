using UnityEngine;

namespace AtmosphericFx
{
	// https://github.com/DefiantZombie/Collide-o-Scope/blob/master/Collide-o-Scope/DrawTools.cs
	internal class DrawingUtils
	{
		private static Material DrawMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended"));

		private static void GLStart()
		{
			GL.PushMatrix();
			DrawMaterial.SetPass(0);
			GL.LoadPixelMatrix();
			GL.Begin(GL.LINES);
		}

		private static void GLEnd()
		{
			GL.End();
			GL.PopMatrix();
		}

		public static Camera GetCamera()
		{
			return FlightCamera.fetch.mainCamera;
		}

		private static void DrawLine(Vector3 origin, Vector3 destination, Color color)
		{
			var screenPoint1 = GetCamera().WorldToScreenPoint(origin);
			var screenPoint2 = GetCamera().WorldToScreenPoint(destination);

			if (screenPoint1.z <= 0 || screenPoint2.z <= 0) return; // Behind us?

			GL.Color(color);
			GL.Vertex3(screenPoint1.x, screenPoint1.y, 0f);
			GL.Vertex3(screenPoint2.x, screenPoint2.y, 0f);
		}

		private static void DrawRay(Vector3 origin, Vector3 direction, Color color)
		{
			var screenPoint1 = GetCamera().WorldToScreenPoint(origin);
			var screenPoint2 = GetCamera().WorldToScreenPoint(origin + direction);

			if (screenPoint1.z <= 0 || screenPoint2.z <= 0) return; // Behind us?

			GL.Color(color);
			GL.Vertex3(screenPoint1.x, screenPoint1.y, 0f);
			GL.Vertex3(screenPoint2.x, screenPoint2.y, 0f);
		}

		public static void DrawArrow(Vector3 origin, Vector3 fwd, Vector3 rt, Vector3 up, Color color)
		{
			GLStart();

			Vector3 target = origin + fwd;
			Vector3 sp = origin + rt * 0.3f;
			Vector3 sn = origin - rt * 0.3f;
			Vector3 tp = origin + up * 0.3f;
			Vector3 tn = origin - up * 0.3f;

			DrawRay(origin, fwd, color);

			DrawLine(sp, target, color);
			DrawLine(sn, target, color);
			DrawLine(tp, target, color);
			DrawLine(tn, target, color);

			DrawLine(sp, origin, color);
			DrawLine(sn, origin, color);
			DrawLine(tp, origin, color);
			DrawLine(tn, origin, color);

			GLEnd();
		}

		public static void DrawAxes(Vector3 origin, Vector3 fwd, Vector3 rt, Vector3 up)
		{
			GLStart();

			DrawRay(origin, fwd.normalized, Color.blue);
			DrawRay(origin, rt.normalized, Color.red);
			DrawRay(origin, up.normalized, Color.green);

			GLEnd();
		}

		public static void DrawBounds(Bounds bounds, Color color)
		{
			var center = bounds.center;

			var x = bounds.extents.x;
			var y = bounds.extents.y;
			var z = bounds.extents.z;

			var topa = center + new Vector3(x, y, z);
			var topb = center + new Vector3(x, y, -z);
			var topc = center + new Vector3(-x, y, z);
			var topd = center + new Vector3(-x, y, -z);

			var bota = center + new Vector3(x, -y, z);
			var botb = center + new Vector3(x, -y, -z);
			var botc = center + new Vector3(-x, -y, z);
			var botd = center + new Vector3(-x, -y, -z);

			GLStart();
			GL.Color(color);

			// Top
			DrawLine(topa, topc, color);
			DrawLine(topa, topb, color);
			DrawLine(topc, topd, color);
			DrawLine(topb, topd, color);

			// Sides
			DrawLine(topa, bota, color);
			DrawLine(topb, botb, color);
			DrawLine(topc, botc, color);
			DrawLine(topd, botd, color);

			// Bottom
			DrawLine(bota, botc, color);
			DrawLine(bota, botb, color);
			DrawLine(botc, botd, color);
			DrawLine(botd, botb, color);

			GLEnd();
		}

		public static void DrawPlane(Vector3 center, Vector2 dimensions, Vector3 rt, Vector3 up, Color color)
		{
			var a = center + dimensions.x * -rt + dimensions.y * up;
			var b = center + dimensions.x * rt + dimensions.y * up;
			var c = center + dimensions.x * rt + dimensions.y * -up;
			var d = center + dimensions.x * -rt + dimensions.y * -up;

			GLStart();
			GL.Color(color);

			DrawLine(a, b, color);
			DrawLine(b, c, color);
			DrawLine(c, d, color);
			DrawLine(d, a, color);

			GLEnd();
		}

		public static void DrawBox(Vector3[] corners, Color color)
		{
			GLStart();
			GL.Color(color);

			DrawLine(corners[0], corners[1], color);
			DrawLine(corners[2], corners[3], color);
			DrawLine(corners[0], corners[2], color);
			DrawLine(corners[1], corners[3], color);

			DrawLine(corners[4], corners[5], color);
			DrawLine(corners[6], corners[7], color);
			DrawLine(corners[4], corners[6], color);
			DrawLine(corners[5], corners[7], color);

			DrawLine(corners[4], corners[0], color);
			DrawLine(corners[5], corners[1], color);
			DrawLine(corners[6], corners[2], color);
			DrawLine(corners[7], corners[3], color);

			GLEnd();
		}
	}
}
