using Godot;

namespace WaveFunctionCollapse.scripts;

[GlobalClass]
public partial class Player : CharacterBody2D {
	private const float Speed = 600.0f;

	public override void _PhysicsProcess(double delta) {
		var xDirection = Input.GetAxis("move_left", "move_right");
		var yDirection = Input.GetAxis("move_up", "move_down");

		Velocity = new Vector2(xDirection, yDirection) * Speed;
		MoveAndSlide();
	}
}
