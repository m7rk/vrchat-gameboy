
using UdonSharp;
using VRC.SDKBase;
using VRC.Udon;
using static BitOps;

public class JOYPAD : UdonSharpBehaviour
{
      private const int JOYPAD_INTERRUPT = 4;
        private const byte PAD_MASK = 0x10;
        private const byte BUTTON_MASK = 0x20;
        private byte pad = 0xF;
        private byte buttons = 0xF;

        internal void handleKeyDown(KeyCode e) {
            byte b = GetKeyBit(e);
            if ((b & PAD_MASK) == PAD_MASK) {
                pad = (byte)(pad & ~(b & 0xF));
            } else if((b & BUTTON_MASK) == BUTTON_MASK) {
                buttons = (byte)(buttons & ~(b & 0xF));
            }
        }

        internal void handleKeyUp(KeyCode e) {
            byte b = GetKeyBit(e);
            if ((b & PAD_MASK) == PAD_MASK) {
                pad = (byte)(pad | (b & 0xF));
            } else if ((b & BUTTON_MASK) == BUTTON_MASK) {
                buttons = (byte)(buttons | (b & 0xF));
            }
        }

        public void update(MMU mmu) {
            byte JOYP = mmu.JOYP();
            if(!isBit(4, JOYP)) {
                mmu.setJOYP((byte)((JOYP & 0xF0) | pad));
                if(pad != 0xF) mmu.requestInterrupt(JOYPAD_INTERRUPT);
            }
            if (!isBit(5, JOYP)) {
                mmu.setJOYP((byte)((JOYP & 0xF0) | buttons));
                if (buttons != 0xF) mmu.requestInterrupt(JOYPAD_INTERRUPT);
            }
            if ((JOYP & 0b00110000) == 0b00110000) mmu.setJOYP(0xFF);
        }

        private byte GetKeyBit(KeyCode e) {
            switch (e) {
                case KeyCode.RIGHT:
                    return 0x11;

                case KeyCode.LEFT:
                    return 0x12;

                case KeyCode.UP:
                    return 0x14;

                case KeyCode.DOWN:
                    return 0x18;

                case KeyCode.A:
                    return 0x21;

                case KeyCode.B:
                    return 0x22;

                case KeyCode.SELECT:
                    return 0x24;

                case KeyCode.START:
                    return 0x28;
            }
            return 0;
        }
    }

