# PokéRedReader
A pokémon data reader for Pokémon Red English version. Any other version of the game will not work and either show fake results or simply crash. Pokémon Blue is not tested, but it should work exactly the same way.

Although "legal" pokémon data *should* show up correctly, glitch pokémon **could** be shown differently that the actual game would. This happens because anything read after memory location 0x8000 is not ROM data, it's basically anything else: RAM, VRAM, SRAM, etc. For this reason, a snapshot is included in this software and loaded for use. You can change it by generating your own and replacing the "pokered.dump" file. Another reason for data to show incorrectly is because of certain quirks in the game's code that I probably didn't take into consideration. Bug reports are welcome :)

# How to build
Install Visual Studio with C# and .NET Framework tools. Install Nuget packages and build. Simple as that.

If you don't want to build your own, you can download releases on this very repository.