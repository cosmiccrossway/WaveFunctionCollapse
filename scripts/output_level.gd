class_name OutputLevel
extends Node2D

@export var input_level: PackedScene
@export var N: int = 2
@export var width: int = 64
@export var height: int = 65
@export var periodicInput: bool = true
@export var periodic: bool = false
@export var symmetry: int = 8
@export var ground: int = 0
@export var limit: int = 0

@export var rotate_dictionary: Dictionary
@export var reflect_dictionary: Dictionary

@onready var output_ground_layer: TileMapLayer = $TileMapLayers/Ground

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	var input: Node2D = input_level.instantiate()
	var model: Model = OverlappingModel.new(input, N, width, height, periodicInput, periodic, symmetry, ground, rotate_dictionary, reflect_dictionary)
	for k in range(0, 10):
		var seed: int = randi()
		var finished = model.run(seed, limit)
		if finished:
			#var x: int = 0
			#var y: int = 0
			#for i in model.patterns.size():
				#var pattern: Array[int] = model.patterns[i]
				#output_ground_layer.set_cell(Vector2i(x, y), output_ground_layer.tile_set.get_source_id(0), model.colors[pattern[0]], 0)
				#output_ground_layer.set_cell(Vector2i(x + 1, y), output_ground_layer.tile_set.get_source_id(0), model.colors[pattern[1]], 0)
				#output_ground_layer.set_cell(Vector2i(x, y + 1), output_ground_layer.tile_set.get_source_id(0), model.colors[pattern[2]], 0)
				#output_ground_layer.set_cell(Vector2i(x + 1, y + 1), output_ground_layer.tile_set.get_source_id(0), model.colors[pattern[3]], 0)
				#x += 3
				#if x > 18:
					#x = 0
					#y += 3
			
			for y in model.fmy:
				var dy: int = 0 if y < model.fmy - model.n + 1 else model.n - 1
				for x in model.fmx:
					var dx: int = 0 if x < model.fmx - model.n + 1 else model.n - 1
					var observed_index: int = x - dx + (y - dy) * model.fmx
					var observed_pattern_index: int = model.observed[observed_index]
					var observed_pattern: Array[int] = model.patterns[observed_pattern_index]
					var pattern_tile_index: int = dx + dy * model.n
					var color_index: int = observed_pattern[pattern_tile_index]
					var c: Vector2i = model.colors[color_index]
					var y_pos = model.fmy - y - 2
					if y_pos > 0:
						output_ground_layer.set_cell(Vector2i(x, model.fmy - y - 2), output_ground_layer.tile_set.get_source_id(0), c, 0)
					
					#var wx = x * 3
					#var wy = (model.fmy - y) * 3
					#output_ground_layer.set_cell(Vector2i(wx, wy), output_ground_layer.tile_set.get_source_id(0), model.colors[observed_pattern[0]], 0)
					#output_ground_layer.set_cell(Vector2i(wx + 1, wy), output_ground_layer.tile_set.get_source_id(0), model.colors[observed_pattern[1]], 0)
					#output_ground_layer.set_cell(Vector2i(wx, wy + 1), output_ground_layer.tile_set.get_source_id(0), model.colors[observed_pattern[2]], 0)
					#output_ground_layer.set_cell(Vector2i(wx + 1, wy + 1), output_ground_layer.tile_set.get_source_id(0), model.colors[observed_pattern[3]], 0)
			
			break


class Model:
	var fmx: int
	var fmy: int
	var n: int
	var dx: Array[int] = [-1, 0, 1, 0]
	var dy: Array[int] = [0, 1, 0, -1]
	var _opposite: Array[int] = [2, 3, 0, 1]
	var wave: Array[Array] = [] # Array[Array[Boolean]]
	var propagator: Array[Array] # Array[Array[Array[int]]]
	var _compatible: Array[Array] # Array[Array[Array[int]]]
	var observed: Array[int]
	var _stack: Array[Array] # Array[Pair[Int, Int]]
	var _random: RandomNumberGenerator
	var weights: Array[float]
	var _weight_log_weights: Array[float]
	var _sums_of_ones: Array[int]
	var _sums_of_weights: Array[float]
	var _sums_of_weight_log_weights: Array[float]
	var _entropies: Array[float]
	var _sum_of_weights: float = 0.0
	var _sum_of_weight_log_weights: float = 0.0
	var _starting_entropy: float = 0.0
	var t_counter: int = 0
	var _stack_size: int = 0
	var periodic: bool = false
	
	func _init(fmx: int, fmy: int) -> void:
		self.fmx = fmx
		self.fmy = fmy
	
	
	func init() -> void:
		wave.resize(fmx * fmy)
		_compatible = []
		_compatible.resize(wave.size())
		
		for i in wave.size():
			wave[i] = []
			wave[i].resize(t_counter)
			_compatible[i] = []
			_compatible[i].resize(t_counter)
			
			for t in t_counter:
				wave[i][t] = false
				_compatible[i][t] = []
				_compatible[i][t].resize(4)
		
		_weight_log_weights = []
		_weight_log_weights.resize(t_counter)
		_sum_of_weights = 0.0
		_sum_of_weight_log_weights = 0.0
		
		for t in t_counter:
			_weight_log_weights[t] = weights[t] * log(weights[t])
			_sum_of_weights += weights[t]
			_sum_of_weight_log_weights += _weight_log_weights[t]
		
		_starting_entropy = log(_sum_of_weights) - _sum_of_weight_log_weights / _sum_of_weights
		
		_sums_of_ones = []
		_sums_of_ones.resize(fmx * fmy)
		_sums_of_weights = []
		_sums_of_weights.resize(fmx * fmy)
		_sums_of_weight_log_weights = []
		_sums_of_weight_log_weights.resize(fmx * fmy)
		_entropies = []
		_entropies.resize(fmx * fmy)
		
		_stack = []
		_stack.resize(wave.size() * t_counter)
		_stack_size = 0
	
	
	func observe(): #nullable bool
		var min: float = 1000.0
		var arg_min: int = -1
		
		for i in wave.size():
			if on_boundary(i % fmx, i / fmx):
				continue
			
			var amount = _sums_of_ones[i]
			if amount == 0:
				return false
			
			var entropy = _entropies[i]
			if amount > 1 and entropy <= min:
				var noise: float = 1E-6 * _random.randf()
				if entropy + noise < min:
					min = entropy + noise
					arg_min = i
		
		if arg_min == -1:
			observed = []
			observed.resize(fmx * fmy)
			for i in wave.size():
				for t in t_counter:
					if wave[i][t] == true:
						observed[i] = t
						break
			return true
		
		var distribution: Array[float] = []
		distribution.resize(t_counter)
		for t in t_counter:
			distribution[t] = weights[t] if wave[arg_min][t] == true else 0.0
		var r: int = random_from_distribution(distribution, _random.randf())
		
		var w = wave[arg_min]
		for t in t_counter:
			if w[t] != (t == r):
				ban(arg_min, t)
		
		return null
	
	
	func random_from_distribution(distribution: Array[float], r: float) -> int:
		var sum: float = 0.0
		for dist in distribution:
			sum += dist
		
		if sum == 0.0:
			for j in distribution.size():
				distribution[j] = 1.0
			for dist in distribution:
				sum += dist
		
		for j in distribution.size():
			distribution[j] /= sum
		
		var i: int = 0
		var x: float = 0.0
		
		while i < distribution.size():
			x += distribution[i]
			if r <= x:
				return i
			i += 1
		
		return 0
	
	
	func ban(i: int, t: int) -> void:
		wave[i][t] = false
		
		var comp = _compatible[i][t]
		for d in range(0, 4):
			comp[d] = 0
		_stack[_stack_size] = [i, t]
		_stack_size += 1
		
		var sum: float = _sums_of_weights[i]
		_entropies[i] += _sums_of_weight_log_weights[i] / sum - log(sum)
		
		_sums_of_ones[i] -= 1
		_sums_of_weights[i] -= weights[t]
		_sums_of_weight_log_weights[i] -= _weight_log_weights[t]
		
		sum = _sums_of_weights[i]
		_entropies[i] -= _sums_of_weight_log_weights[i] / sum - log(sum)
	
	
	func propogate() -> void:
		while _stack_size > 0:
			var e1 = _stack[_stack_size - 1]
			_stack_size -= 1
			
			var i1 = e1[0]
			var x1 = i1 % fmx
			var y1 = i1 / fmx
			
			for d in range(0, 4):
				var dx = self.dx[d]
				var dy = self.dy[d]
				var x2 = x1 + dx
				var y2 = y1 - dy
				if on_boundary(x2, y2):
					continue
				
				if x2 < 0:
					x2 += fmx
				elif x2 >= fmx:
					x2 -= fmx
				
				if y2 < 0:
					y2 += fmy
				elif y2 >= fmy:
					y2 -= fmy
				
				var i2 = x2 + y2 * fmx
				var p = propagator[d][e1[1]]
				var compat = _compatible[i2]
				
				for l in p.size():
					var t2 = p[l]
					var comp = compat[t2]
					
					comp[d] -= 1
					if comp[d] == 0:
						ban(i2, t2)
	
	
	func run(seed: int, limit: int) -> bool:
		if wave.size() == 0:
			init()
		
		clear()
		_random = RandomNumberGenerator.new()
		_random.seed = seed
		
		var l = 0
		while (l < limit or limit == 0):
			var result = observe()
			if result != null:
				return result
			propogate()
			l += 1
		
		return true
	
	
	func clear() -> void:
		for i in wave.size():
			for t in t_counter:
				wave[i][t] = true
				for d in range(0, 4):
					_compatible[i][t][d] = propagator[_opposite[d]][t].size()
			
			_sums_of_ones[i] = weights.size()
			_sums_of_weights[i] = _sum_of_weights
			_sums_of_weight_log_weights[i] = _sum_of_weight_log_weights
			_entropies[i] = _starting_entropy
	
	
	func on_boundary(x: int, y: int) -> bool:
		return  not periodic and (x + n > fmx or y + n > fmy or x < 0 or y < 0)


class OverlappingModel extends Model:
	var patterns: Array[Array] #Array[Array[Vector2i]]
	var colors: Array[Vector2i]
	var ground: int
	
	func _init(input_level: Node2D, n: int, width: int, height: int, periodic_input: bool,
			periodic_output: bool, symmetry: int, ground: int, 
			rotate_dictionary: Dictionary, reflect_dictionary: Dictionary) -> void:
		super._init(width, height)
		self.n = n
		periodic = periodic_output
		
		var ground_layer: TileMapLayer = input_level.find_child("TileMapLayers").find_child("Ground")
		var dimensions := ground_layer.get_used_rect()
		
		var smx: int = dimensions.size.x
		var smy: int = dimensions.size.y
		
		var sample: Array[Array] = [] #Array[Array[int]]
		colors = []
		
		for y in smy:
			for x in smx:
				var color: Vector2i = ground_layer.get_cell_atlas_coords(Vector2i(x, y))
				var i = colors.find(color)
				if i == -1:
					colors.append(color)
					i = colors.size() - 1
				if sample.size() <= x:
					sample.append([])
				sample[x].append(i)
		
		var c: int = colors.size()
		var w: float = pow(float(c), float(n * n))
		
		var rotate_colors: Dictionary = {}
		var reflect_colors: Dictionary = {}
		
		for i in colors.size():
			var atlas_coords_key = colors[i]
			for j in colors.size():
				if colors[j] == rotate_dictionary[atlas_coords_key]:
					rotate_colors[i] = j
				if colors[j] == reflect_dictionary[atlas_coords_key]:
					reflect_colors[i] = j
		
		var pattern: Callable = func (passed_in_func: Callable) -> Array[int]:
			var result: Array[int] = []
			result.resize(n * n)
			for y in n:
				for x in n:
					result[x + y * n] = passed_in_func.call(x, y) as int
			return result
		
		var pattern_from_sample: Callable = func (x: int, y: int) -> Array[int]:
			return pattern.call(
				func (dx: int, dy: int) -> int:
					return sample[(x + dx) % smx][(y + dy) % smy]
			)
		
		var rotate: Callable = func (p: Array[int]) -> Array[int]:
			return pattern.call(
				func (x: int, y: int) -> int:
					return rotate_colors[p[n - 1 - y + x * n]]
			)
		
		var reflect: Callable = func (p: Array[int]) -> Array[int]:
			return pattern.call(
				func (x: int, y: int) -> int:
					return reflect_colors[p[n - 1 - x + y * n]]
			)
		
		var index: Callable = func (p: Array[int]) -> int:
			var result: int = 0
			var power: int = 1
			for i in p.size():
				result += p[p.size() - 1 - i] * power
				power *= c
			return result
		
		var pattern_from_index: Callable = func(ind: int) -> Array[int]:
			var residue = ind
			var power = int(w)
			var result: Array[int] = []
			result.resize(n * n)
			
			for i in result.size():
				power /= c
				var count = 0
				
				while residue >= power:
					residue -= power
					count += 1
				
				result[i] = count
			return result
		
		var weights: Dictionary = {}
		var ordering: Array[int] = []
		
		for y in smy if periodic_input else smy - n + 1:
			for x in smx if periodic_input else smx - - + 1:
				var ps: Array[Array] = []
				ps.resize(8)
				ps[0] = pattern_from_sample.call(x, y)
				ps[1] = reflect.call(ps[0])
				ps[2] = rotate.call(ps[0])
				ps[3] = reflect.call(ps[2])
				ps[4] = rotate.call(ps[2])
				ps[5] = reflect.call(ps[4])
				ps[6] = rotate.call(ps[4])
				ps[7] = reflect.call(ps[6])
				
				for k in symmetry:
					var ind = index.call(ps[k])
					if weights.keys().has(ind):
						weights[ind] = weights[ind] + 1
					else:
						weights[ind] = 1
						ordering.append(ind)
		
		t_counter = weights.size()
		self.ground = (ground + t_counter) % t_counter
		patterns = []
		patterns.resize(t_counter)
		self.weights = []
		self.weights.resize(t_counter)
		
		for counter in ordering.size():
			var order_item = ordering[counter]
			patterns[counter] = pattern_from_index.call(order_item)
			self.weights[counter] = weights[order_item]
		
		var agrees: Callable = func (p1: Array[int], p2: Array[int], dx: int, dy: int) -> bool:
			var x_min = 0 if dx < 0 else dx
			var x_max = dx + n if dx < 0 else n
			var y_min = 0 if dy < 0 else dy
			var y_max = dy + n if dy < 0 else n
			
			for y in range(y_min, y_max):
				for x in range(x_min, x_max):
					if p1[x + n * y] != p2[x - dx + n * (y - dy)]:
						return false
			return true
		
		propagator = []
		propagator.resize(4)
		for d in range(0, 4):
			propagator[d] = []
			propagator[d].resize(t_counter)
			for t in t_counter:
				var list: Array[int] = []
				for t2 in t_counter:
					if agrees.call(patterns[t], patterns[t2], dx[d], dy[d]):
						list.append(t2)
				propagator[d][t] = list
	
	
	func clear() -> void:
		super.clear()
		
		if ground != 0:
			for x in fmx:
				for t in t_counter:
					if t != ground:
						ban(x + (fmy - 1) * fmx, t)
				for y in fmy - 1:
					ban(x + y * fmx, ground)
