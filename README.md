## What's this?

This is an attempt at automating the generation of [Nuke](https://nuke.build/) task classes and specification jsons for arbitrary command line tools that depend on the package [CommandDotNet](https://commanddotnet.bilal-fazlani.com/).

At first I tried the idea of inspecting the command graph without actually running the entire pipeline, but that doesn't seem to be supported in CommandDotNet.
So the hack I did was to add a custom task that could work with the `CommandContext` — a god object with all configuration, runtime parameters and caches, the arguments the application received and even more crap, while all I actually cared about was the command info.

In order to be able to generate the json and Nuke's code, I used the type definitions available in `Nuke.CodeGeneration`.
So it was just about reading the command graph in the format that CommandDotNet provides and converting that manually to Nuke's.

`CommandDotNetNuke.Test` contains an example application that uses the task. It only compiles the task into the app if a corresponding constant is enabled (propagated to C# via MSBuild), otherwise it only compiles the other tasks defined in the app.
It uses a fake dependency resolver to be able to inject the configuration into the task (I'm not using proper dependency injection, because it was funky to integrate with CommandDotNet and I kinda left it alone).
The meh thing is that the configuration is baked into the application, I can't e.g. override some values from the command line.
I don't think the library is capable of doing both getting arguments from the command line, and taking the defaults from a given object that it would get from dependency injection without it being wacky to implement.

I have checked out out some more libraries and most of them either have inconvenient API, or their code is weird or messy.
For example the code of [this lib](https://github.com/commandlineparser/commandline) is written in SQL Linq style, and it's abusing functional features.
[The lib that I used here](https://commanddotnet.bilal-fazlani.com/) works with a single context, dumping everything into that one object, and is not exactly modular — it expects the entire pipeline that it suggests to be used.
My usecase evntually doesn't really follow the steps that they expect there, so it's kind of messy to think about.

The project `CommandDotNetNuke.Converter` contains the task and some glue code that fills in the Nuke's `Tool` type.
It can be used standalone as a NuGet package (which I didn't release, obviously).

And then there's the Nuke build files, which define the workflow for getting those generated files to be put into the separate project `NukeGeneratedCode`:
1. Build the converter thing;
2. Build the CLI test app with that constant defined that makes it import the converter;
3. Run the built CLI test app with `nuke-describe` as an argument, which invokes the conversion task, with `NukeGeneratedCode` project directory as the working directory;
4. Build the project `NukeGeneratedCode`. It could be packed as a `NuGet` now and reused for Nuke builds that need to call the CLI app.

## Was it worth the effort?

**No.**

If the target app is already C#, then it would probably be easier to just make it as modular to the extent of you being able to just dynamically link to it, fill in a configuration structure and just call the desired function (probably via an interface, so that you could manually dynamically link to a dll while having compiled it from the same Nuke process, from within Nuke).
This way there's no need to be juggling the arguments via the command line, converting them to strings and then reading and parsing them back again.

Another way would be to write the configuration classes manually.
I don't really like the code that Nuke generates anyway.

The complexity that generating these files brings and the fact that you have to write glue code for any CLI library that you're going to use, is just not worth it in this case IMO.

And if the app is not C# but some other language, you'd still need to figure out the Tool's datastructure that would generate the json that you need.
And then you need a way to call Nuke's generator function on that json, so you'd need another CLI app that does that.
It gets pretty involved from the build complexity point of view.
