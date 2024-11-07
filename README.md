This was an experiment in implementing [wave function collapse](https://github.com/mxgmn/WaveFunctionCollapse) in GDScript in Godot for the for the [ProcJam 2024](https://itch.io/jam/procjam).

Note that GDScript is quite slow, which leads to a major stall on startup.  There were are a couple of bugs in the implementation, which I did some hacks to the processing at the end in order 
to compensate for. It's not worth my time to go back and fix these, due to performance issues, and instead a rewrite in either C# or a GDExtension in C++ would instead be the potental way to
move forward with this.
