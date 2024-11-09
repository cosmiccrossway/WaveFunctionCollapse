using System;
using Godot;

namespace WaveFunctionCollapse.scripts;

public partial class Camera : Camera2D {
	private Vector2 _zoomMin = new Vector2(0.2f, 0.2f);
	private Vector2 _zoomMax = new Vector2(2f, 2f);
	private int _zoomSpeed = 1;
	
	private bool _zoomIn = false;
	private bool _zoomOut = false;

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override async void _Process(double delta) {
		if (Input.IsActionJustPressed("zoom_in") && Zoom < _zoomMax) {
			_zoomIn = true;
			_zoomOut = false;
		}

		if (Input.IsActionJustPressed("zoom_out") && Zoom > _zoomMin) {
			_zoomOut = true;
			_zoomIn = false;
		}

		if (_zoomIn) {
			var zoomTo = Zoom * 1.05f;
			if (zoomTo > _zoomMax) {
				zoomTo = _zoomMax;
			}
			Zoom = Zoom.Lerp(zoomTo, _zoomSpeed);
			await ToSignal(GetTree().CreateTimer(0.10), "timeout");
			_zoomIn = false;
		}

		if (_zoomOut) {
			var zoomTo = Zoom * 0.95f;
			if (zoomTo < _zoomMin) {
				zoomTo = _zoomMin;
			}
			Zoom = Zoom.Lerp(zoomTo, _zoomSpeed);
			await ToSignal(GetTree().CreateTimer(0.10), "timeout");
			_zoomOut = false;
		}
	}
}
