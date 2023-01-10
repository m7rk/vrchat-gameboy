# vrchat-gameboy
Gameboy Emulator built entirely inside VRChat


This is a gameboy emulator built inside VRChat using UdonSharp. It is heavily based on BlueStorm's ProjectDMG: https://github.com/BluestormDNA/ProjectDMG


I cannot guarantee that it is stable, but it passes the Blargg CPU tests (https://github.com/retrio/gb-test-roms) and it seems to boot into pokemon blue just fine.


The problem is that this emulator runs VERY, very slowly, this is due to Udon's VM and I'm not entirely sure if it can be optimized. We would need the Udon bytecode to be about 100x faster before this becomes _remotely_ playable.


I will probably tinker with this in the future, but if any Udon wizards think they can get it running faster before I can, PR's welcome :)
