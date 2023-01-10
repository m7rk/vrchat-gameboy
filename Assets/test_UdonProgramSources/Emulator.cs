
using System;
using System.Diagnostics;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

public class Emulator : UdonSharpBehaviour
{
  // consts
        public const int DMG_4Mhz = 4194304;
        // a factor of 100x ..... should be 50.
        public const float REFRESH_RATE = 5000f;
        public const int CYCLES_PER_UPDATE = (int)(DMG_4Mhz / REFRESH_RATE);

        // hardware
        public CPU cpu;
        public MMU mmu;
        public PPU ppu;
        public Timer timer;
        public JOYPAD joypad;

        // the ROM data
        public TextAsset rom;

        // timing junk
        long start;
        long elapsed;
        int cpuCycles;
        int cyclesThisUpdate;
        Stopwatch timerCounter = new Stopwatch();

        public void POWER_ON()
        {
            mmu.loadGamePak(rom.bytes);
        }


        private bool started = false;
        int fpsCounter;
        int ctrMtonic = 0;

        public void Start()
        {
            // Main Loop setup.
            start = nanoTime();
            elapsed = 0;
            cpuCycles = 0;
            cyclesThisUpdate = 0;
            timerCounter.Start();
        }

        public void FixedUpdate()
        {
            if(!started)
            {
                POWER_ON();
                started = true;
            }
            EXECUTE();
        }



        public void EXECUTE() {

            if (timerCounter.ElapsedMilliseconds > 1000) {
                timerCounter.Restart();
                fpsCounter = 0;
                UnityEngine.Debug.Log("total instr: " + ctrMtonic);
            }

            while (cyclesThisUpdate < CYCLES_PER_UPDATE) {
                cpuCycles = cpu.Exe();
                cyclesThisUpdate += cpuCycles;
                timer.update(cpuCycles, mmu);
                ppu.update(cpuCycles, mmu);
                joypad.update(mmu);
                handleInterrupts();
                ctrMtonic++;
            }
            fpsCounter++;
            cyclesThisUpdate -= CYCLES_PER_UPDATE;
        }

        private void handleInterrupts() {
            byte IE = mmu.IE();
            byte IF = mmu.IF();
            for (int i = 0; i < 5; i++) {
                if ((((IE & IF) >> i) & 0x1) == 1) {
                    cpu.ExecuteInterrupt(i);
                }
            }

            cpu.UpdateIME();
        }

        private static long nanoTime() {
            long nano = 10000L * Stopwatch.GetTimestamp();
            nano /= TimeSpan.TicksPerMillisecond;
            nano *= 100L;
            return nano;
        }
}
