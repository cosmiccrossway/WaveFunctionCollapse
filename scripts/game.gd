extends Node2D

@export var output_level_scene: PackedScene

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	var enter: bool = Input.is_action_just_pressed("enter")
	if enter:
		var output_level: OutputLevel = output_level_scene.instantiate()
		add_child(output_level)
