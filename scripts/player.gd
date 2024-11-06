extends CharacterBody2D

@onready var camera_2d: Camera2D = $Camera2D

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _physics_process(_delta: float) -> void:
	var x_direction := Input.get_axis("move_left", "move_right")
	var y_direction := Input.get_axis("move_up", "move_down")
	var z_direction := Input.get_axis("zoom_out", "zoom_in")
	
	camera_2d.zoom += Vector2(z_direction, z_direction) * 0.05
	
	velocity = Vector2(x_direction, y_direction).normalized() * 600.0
	move_and_slide()
