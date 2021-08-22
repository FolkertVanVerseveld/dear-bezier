using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImPlotNET;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

using static ImGuiNET.ImGuiNative;

namespace ImGuiNET
{
	class BezierPath
	{
		public List<Vector3> points = new List<Vector3>();
		public List<Vector3> gfx_points = new List<Vector3>();

		float x = 100, y = 100, w = 1.0f;
		int point_index = 0, selected = -1;
		double detail = 0.1;

		float path_length = 0, pos_frac = 0;
		bool path_use_frac = true;

		static readonly float pt_radius = 4.0f;

		/// <summary>
		/// Bereken tussenpunten voor de gegeven bezierpunten controls met de stapgrootte detail.
		/// </summary>
		/// <param name="controls"></param>
		/// <param name="detail"></param>
		/// <returns></returns>
		List<Vector3> continuous_curve(List<Vector3> controls, double detail)
		{
			if (detail < 0 || detail > 1)
				throw new ArgumentOutOfRangeException();

			List<Vector3> renderingPoints = new List<Vector3>();
			List<Vector3> controlPoints = new List<Vector3>();
			//generate the end and control points
			for (int i = 1; i < controls.Count - 1; i += 2) {
				controlPoints.Add(center(controls[i - 1], controls[i]));
				controlPoints.Add(controls[i]);
				controlPoints.Add(controls[i + 1]);

				if (i + 2 < controls.Count - 1) {
					controlPoints.Add(center(controls[i + 1], controls[i + 2]));
				}
			}

			//Generate the detailed points.
			Vector3 a0, a1, a2, a3;

			for (int i = 0; i < controlPoints.Count - 2; i += 4) {
				a0 = controlPoints[i];
				a1 = controlPoints[i + 1];
				a2 = controlPoints[i + 2];

				if (i + 3 > controlPoints.Count - 1) {
					//quad
					for (double j = 0; j < 1; j += detail) {
						renderingPoints.Add(quadBezier(a0, a1, a2, j));
					}
				} else {
					//cubic
					a3 = controlPoints[i + 3];

					for (double j = 0; j < 1; j += detail) {
						renderingPoints.Add(cubicBezier(a0, a1, a2, a3, j));
					}
				}
			}

			return renderingPoints;
		}

		// interne functies voor curve berekeningen

		private static Vector3 cubicBezier(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, double t) {
			return new Vector3(
				cubicBezierPoint(p1.X, p2.X, p3.X, p4.X, t),
				cubicBezierPoint(p1.Y, p2.Y, p3.Y, p4.Y, t),
				cubicBezierPoint(p1.Z, p2.Z, p3.Z, p4.Z, t)
			);
		}

		private static Vector3 quadBezier(Vector3 p1, Vector3 p2, Vector3 p3, double t) {
			return new Vector3(
				quadBezierPoint(p1.X, p2.X, p3.X, t),
				quadBezierPoint(p1.Y, p2.Y, p3.Y, t),
				quadBezierPoint(p1.Z, p2.Z, p3.Z, t)
			);
		}

		private static float cubicBezierPoint(float a0, float a1, float a2, float a3, double t) {
			return (float)(Math.Pow(1 - t, 3) * a0 + 3* Math.Pow(1 - t, 2) * t * a1 + 3 * (1 - t) * Math.Pow(t, 2) * a2 + Math.Pow(t, 3) * a3);
		}

		private static float quadBezierPoint(float a0, float  a1, float a2, double t) {
			return (float)(Math.Pow(1 - t, 2) * a0 + 2 * (1 - t) * t * a1 + Math.Pow(t, 2) * a2);
		}

		public static Vector3 center(Vector3 p1, Vector3 p2) {
			return new Vector3(
				(p1.X + p2.X) / 2,
				(p1.Y + p2.Y) / 2,
				(p1.Z + p2.Z) / 2
			);
		}

		public void show_path()
		{
			var gfx = ImGui.GetForegroundDrawList();
			uint col = ImGui.GetColorU32(new Vector4(0.0f, 1.0f, 0.0f, 1.0f));
			uint col2 = ImGui.GetColorU32(new Vector4(1.0f, 0.0f, 0.0f, 1.0f));

			if (points.Count < 4)
			{
				switch (points.Count)
				{
					case 2:
						{
							Vector2 from = new Vector2(points[0].X, points[0].Y), to = new Vector2(points[1].X, points[1].Y);
							gfx.AddLine(from, to, col, w);
						}
						break;
					case 3:
						{
							Vector2 p0 = new Vector2(points[0].X, points[0].Y), p1 = new Vector2(points[1].X, points[1].Y), p2 = new Vector2(points[2].X, points[2].Y);
							gfx.AddBezierQuadratic(p0, p1, p2, col, w);
						}
						break;
				}
			}
			else
			{
				for (int i = 0; i < gfx_points.Count - 1; ++i)
				{
					Vector2 from = new Vector2(gfx_points[i].X, gfx_points[i].Y), to = new Vector2(gfx_points[i + 1].X, gfx_points[i + 1].Y);
					gfx.AddLine(from, to, col, w);
				}
			}

			foreach (Vector3 pt3 in points)
			{
				Vector2 pt = new Vector2(pt3.X, pt3.Y);
				gfx.AddCircleFilled(pt, pt_radius, col2);
			}
		}

		int find_selected_point(Vector2 m)
		{
			for (int i = 0; i < points.Count; ++i)
			{
				Vector2 pt = new Vector2(points[i].X, points[i].Y);
				float d = (pt - m).Length();
				if (d <= pt_radius)
					return i;
			}

			return -1;
		}

		Vector3 find_point_in_path_frac(float frac)
		{
			if (frac < 0) frac = 0;
			if (frac > 1) frac = 1;
			float length = 0, pos = frac * path_length;

			for (int i = 0; i < gfx_points.Count - 1; ++i)
			{
				float n = (gfx_points[i + 1] - gfx_points[i]).Length();
				float next_length = length + n;

				if (next_length >= pos)
				{
					float t = (next_length - pos) / n;
					return gfx_points[i] + (1 - t) * (gfx_points[i + 1] - gfx_points[i]);
				}

				length = next_length;
			}

			return gfx_points[0];
		}

		Vector3 find_point_in_path_pos(float pos)
		{
			if (pos < 0) pos = 0;
			if (pos > path_length) pos = path_length;
			float length = 0;

			for (int i = 0; i < gfx_points.Count - 1; ++i)
			{
				float n = (gfx_points[i + 1] - gfx_points[i]).Length();
				float next_length = length + n;

				if (next_length >= pos)
				{
					float t = (next_length - pos) / n;
					return gfx_points[i] + (1 - t) * (gfx_points[i + 1] - gfx_points[i]);
				}

				length = next_length;
			}

			return gfx_points[0];
		}

		public void show()
		{
			ImGui.Begin("Bezier padvolger dinges");
			{
				ImGui.Text($"Aantal punten: {points.Count}");

				bool changed = false;

				ImGui.InputFloat("X", ref x);
				ImGui.InputFloat("Y", ref y);
				ImGui.InputFloat("W", ref w);

				changed |= ImGui.InputDouble("Step size", ref detail);

				if (detail < 0.001)
					detail = 0.001;
				if (detail > 0.5)
					detail = 0.5;

				if (ImGui.Button("Weg ermee"))
				{
					points.Clear();
				}

				ImGui.SameLine();

				if (ImGui.Button("Voeg toe"))
				{
					changed = true;
					points.Add(new Vector3(x, y, 0.0f));
				}

				if (points.Count > 0)
				{
					Vector2 m = ImGui.GetMousePos();

					if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
					{
						if (selected < 0)
							selected = find_selected_point(m);
					}
					else
					{
						selected = -1;
					}

					if (selected >= 0 && selected < points.Count)
					{
						if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
						{
							selected = -1;
						}
						else
						{
							points[selected] = new Vector3(m.X, m.Y, points[selected].Z);
							changed |= true;
						}
					}

					ImGui.SliderInt("Puntpositie", ref point_index, 0, points.Count - 1);

					if (point_index >= 0 && point_index < points.Count)
					{
						Vector3 p = points[point_index];
						ImGui.Text($"X: {p.X}, Y: {p.Y}");
					}
				}

				ImGui.Text($"selected = {selected}");

				if (changed)
				{
					gfx_points = continuous_curve(points, detail);
					path_length = get_length();
				}

				show_path();

				if (gfx_points.Count > 1)
				{
					ImGui.Text($"Path length: {path_length}");
					ImGui.Checkbox("Use fraction [0,1]", ref path_use_frac);

					if (path_use_frac)
						ImGui.SliderFloat("Ball position", ref pos_frac, 0.0f, 1.0f);
					else
						ImGui.SliderFloat("Ball position", ref pos_frac, 0.0f, path_length);

					var gfx = ImGui.GetForegroundDrawList();
					uint col = ImGui.GetColorU32(new Vector4(0.0f, 0.0f, 1.0f, 1.0f));

					Vector3 ball_pos = Vector3.Zero;

					if (path_use_frac)
						ball_pos = find_point_in_path_frac(pos_frac);
					else
						ball_pos = find_point_in_path_pos(pos_frac);

					gfx.AddCircleFilled(new Vector2(ball_pos.X, ball_pos.Y), 8.0f, col);
				}

			}
			ImGui.End();
		}

		private float get_curve_length(List<Vector3> points)
		{
			float length = 0;

			for (int i = 0; i < points.Count - 1; ++i)
				length += (points[i + 1] - points[i]).Length();

			return length;
		}

		public float get_length()
		{
			return points.Count < 4 ? get_curve_length(points) : get_curve_length(gfx_points);
		}
	}

	class Program
	{
		private static Sdl2Window _window;
		private static GraphicsDevice _gd;
		private static CommandList _cl;
		private static ImGuiController _controller;
		private static MemoryEditor _memoryEditor;

		// UI state
		private static float _f = 0.0f;
		private static Vector3 _clearColor = new Vector3(0.45f, 0.55f, 0.6f);
		private static bool _showImGuiDemoWindow = false;
		private static bool _showAnotherWindow = false;
		private static bool _showMemoryEditor = false;
		private static byte[] _memoryEditorData;
		private static uint s_tab_bar_flags = (uint)ImGuiTabBarFlags.Reorderable;
		static bool[] s_opened = { true, true, true, true }; // Persistent user state

		static void SetThing(out float i, float val) { i = val; }

		static BezierPath bezier_widget = new BezierPath();

		static void Main(string[] args)
		{
			// Create window, GraphicsDevice, and all resources necessary for the demo.
			VeldridStartup.CreateWindowAndGraphicsDevice(
				new WindowCreateInfo(50, 50, 1280, 720, WindowState.Normal, "ImGui.NET Sample Program"),
				new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
				out _window,
				out _gd);
			_window.Resized += () =>
			{
				_gd.MainSwapchain.Resize((uint)_window.Width, (uint)_window.Height);
				_controller.WindowResized(_window.Width, _window.Height);
			};
			_cl = _gd.ResourceFactory.CreateCommandList();
			_controller = new ImGuiController(_gd, _gd.MainSwapchain.Framebuffer.OutputDescription, _window.Width, _window.Height);
			_memoryEditor = new MemoryEditor();
			Random random = new Random();
			_memoryEditorData = Enumerable.Range(0, 1024).Select(i => (byte)random.Next(255)).ToArray();

			// Main application loop
			while (_window.Exists)
			{
				InputSnapshot snapshot = _window.PumpEvents();
				if (!_window.Exists) { break; }
				_controller.Update(1f / 60f, snapshot); // Feed the input events to our ImGui controller, which passes them through to ImGui.

				SubmitUI();

				_cl.Begin();
				_cl.SetFramebuffer(_gd.MainSwapchain.Framebuffer);
				_cl.ClearColorTarget(0, new RgbaFloat(_clearColor.X, _clearColor.Y, _clearColor.Z, 1f));
				_controller.Render(_gd, _cl);
				_cl.End();
				_gd.SubmitCommands(_cl);
				_gd.SwapBuffers(_gd.MainSwapchain);
			}

			// Clean up Veldrid resources
			_gd.WaitForIdle();
			_controller.Dispose();
			_cl.Dispose();
			_gd.Dispose();
		}

		private static unsafe void SubmitUI()
		{
			// Demo code adapted from the official Dear ImGui demo program:
			// https://github.com/ocornut/imgui/blob/master/examples/example_win32_directx11/main.cpp#L172

			// 1. Show a simple window.
			// Tip: if we don't call ImGui.BeginWindow()/ImGui.EndWindow() the widgets automatically appears in a window called "Debug".
			{
				//ImGui.Text("Hello, world!");                                        // Display some text (you can use a format string too)
				//ImGui.SliderFloat("float", ref _f, 0, 1, _f.ToString("0.000"));  // Edit 1 float using a slider from 0.0f to 1.0f
				//ImGui.ColorEdit3("clear color", ref _clearColor);                   // Edit 3 floats representing a color

				//ImGui.Text($"Mouse position: {ImGui.GetMousePos()}");

				ImGui.Checkbox("ImGui Demo Window", ref _showImGuiDemoWindow);                 // Edit bools storing our windows open/close state
				ImGui.Checkbox("Another Window", ref _showAnotherWindow);
				ImGui.Checkbox("Memory Editor", ref _showMemoryEditor);

				float framerate = ImGui.GetIO().Framerate;
				ImGui.Text($"Application average {1000.0f / framerate:0.##} ms/frame ({framerate:0.#} FPS)");
			}

			// 3. Show the ImGui demo window. Most of the sample code is in ImGui.ShowDemoWindow(). Read its code to learn more about Dear ImGui!
			if (_showImGuiDemoWindow)
			{
				// Normally user code doesn't need/want to call this because positions are saved in .ini file anyway.
				// Here we just want to make the demo initial state a bit more friendly!
				ImGui.SetNextWindowPos(new Vector2(650, 20), ImGuiCond.FirstUseEver);
				ImGui.ShowDemoWindow(ref _showImGuiDemoWindow);
			}

			if (ImGui.TreeNode("Tabs"))
			{
				if (ImGui.TreeNode("Basic"))
				{
					ImGuiTabBarFlags tab_bar_flags = ImGuiTabBarFlags.None;
					if (ImGui.BeginTabBar("MyTabBar", tab_bar_flags))
					{
						if (ImGui.BeginTabItem("Avocado"))
						{
							ImGui.Text("This is the Avocado tab!\nblah blah blah blah blah");
							ImGui.EndTabItem();
						}
						if (ImGui.BeginTabItem("Broccoli"))
						{
							ImGui.Text("This is the Broccoli tab!\nblah blah blah blah blah");
							ImGui.EndTabItem();
						}
						if (ImGui.BeginTabItem("Cucumber"))
						{
							ImGui.Text("This is the Cucumber tab!\nblah blah blah blah blah");
							ImGui.EndTabItem();
						}
						ImGui.EndTabBar();
					}
					ImGui.Separator();
					ImGui.TreePop();
				}

				if (ImGui.TreeNode("Advanced & Close Button"))
				{
					// Expose a couple of the available flags. In most cases you may just call BeginTabBar() with no flags (0).
					ImGui.CheckboxFlags("ImGuiTabBarFlags_Reorderable", ref s_tab_bar_flags, (uint)ImGuiTabBarFlags.Reorderable);
					ImGui.CheckboxFlags("ImGuiTabBarFlags_AutoSelectNewTabs", ref s_tab_bar_flags, (uint)ImGuiTabBarFlags.AutoSelectNewTabs);
					ImGui.CheckboxFlags("ImGuiTabBarFlags_NoCloseWithMiddleMouseButton", ref s_tab_bar_flags, (uint)ImGuiTabBarFlags.NoCloseWithMiddleMouseButton);
					if ((s_tab_bar_flags & (uint)ImGuiTabBarFlags.FittingPolicyMask) == 0)
						s_tab_bar_flags |= (uint)ImGuiTabBarFlags.FittingPolicyDefault;
					if (ImGui.CheckboxFlags("ImGuiTabBarFlags_FittingPolicyResizeDown", ref s_tab_bar_flags, (uint)ImGuiTabBarFlags.FittingPolicyResizeDown))
				s_tab_bar_flags &= ~((uint)ImGuiTabBarFlags.FittingPolicyMask ^ (uint)ImGuiTabBarFlags.FittingPolicyResizeDown);
					if (ImGui.CheckboxFlags("ImGuiTabBarFlags_FittingPolicyScroll", ref s_tab_bar_flags, (uint)ImGuiTabBarFlags.FittingPolicyScroll))
				s_tab_bar_flags &= ~((uint)ImGuiTabBarFlags.FittingPolicyMask ^ (uint)ImGuiTabBarFlags.FittingPolicyScroll);

					// Tab Bar
					string[] names = { "Artichoke", "Beetroot", "Celery", "Daikon" };

					for (int n = 0; n < s_opened.Length; n++)
					{
						if (n > 0) { ImGui.SameLine(); }
						ImGui.Checkbox(names[n], ref s_opened[n]);
					}

					// Passing a bool* to BeginTabItem() is similar to passing one to Begin(): the underlying bool will be set to false when the tab is closed.
					if (ImGui.BeginTabBar("MyTabBar", (ImGuiTabBarFlags)s_tab_bar_flags))
					{
						for (int n = 0; n < s_opened.Length; n++)
							if (s_opened[n] && ImGui.BeginTabItem(names[n], ref s_opened[n]))
							{
								ImGui.Text($"This is the {names[n]} tab!");
								if ((n & 1) != 0)
									ImGui.Text("I am an odd tab.");
								ImGui.EndTabItem();
							}
						ImGui.EndTabBar();
					}
					ImGui.Separator();
					ImGui.TreePop();
				}
				ImGui.TreePop();
			}

			bezier_widget.show();

			ImGuiIOPtr io = ImGui.GetIO();
			SetThing(out io.DeltaTime, 2f);

			if (_showMemoryEditor)
			{
				_memoryEditor.Draw("Memory Editor", _memoryEditorData, _memoryEditorData.Length);
			}
		}
	}
}
