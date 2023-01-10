
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using static BitOps;

public class CPU : UdonSharpBehaviour
{
        public Cycles cyclesUtil;
        public MMU mmu;
        private ushort PC;
        private ushort SP;

        private byte A, B, C, D, E, F, H, L;

        private ushort getAF() { return (ushort)(A << 8 | F); }
        private void setAF(ushort value) { A = (byte)(value >> 8); F = (byte)(value & 0xF0); }

        private ushort getBC() { return (ushort)(B << 8 | C); }

        private void setBC(ushort value) { B = (byte)(value >> 8); C = (byte)(0xff & value); }

        private ushort getDE() { return (ushort)(D << 8 | E); }
        private void setDE(ushort value) { D = (byte)(value >> 8); E = (byte)(0xff & value); }

        private ushort getHL() { return (ushort)(H << 8 | L); }
        private void setHL(ushort value) { H = (byte)(value >> 8); L = (byte)(0xff & value); }

        // hl++
        private ushort HLIncr()
        {
            var ret = getHL();
            setHL((ushort)(0xffff & (getHL() + 1)));
            return ret;
        }

        private ushort HLDecr()
        {
            var ret = getHL();
            setHL((ushort)(0xffff & (getHL() - 1)));
            return ret;
        }


        private bool getFlagZ() { return (F & 0x80) != 0; } 
        private void setFlagZ(bool value){ F = value ? (byte)(F | 0x80) : (byte)(F & ~0x80); }

        private bool getFlagN() { return (F & 0x40) != 0; }
        private void setFlagN(bool value) { F = value ? (byte)(F | 0x40) : (byte)(F & ~0x40); } 

        private bool getFlagH() { return (F & 0x20) != 0; }
        private void setFlagH(bool value) { F = value ? (byte)(F | 0x20) : (byte)(F & ~0x20); } 

        private bool getFlagC() { return (F & 0x10) != 0; }
        private void setFlagC(bool value) { F = value ? (byte)(F | 0x10) : (byte)(F & ~0x10); } 

        private bool IME;
        private bool IMEEnabler;
        private bool HALTED;
        private bool HALT_BUG;
        private int cycles;

        public void Start()
        {
            setAF(0x01B0);
            setBC(0x0013);
            setDE(0x00D8);
            setHL(0x014d);
            SP = 0xFFFE;
            PC = 0x100;
        }

        public int Exe() {

            byte opcode = mmu.readByte(PC++);
            if (HALT_BUG) {
                PC--;
                HALT_BUG = false;
            }
            //debug(opcode);
            cycles = 0;

            switch (opcode) {
                case 0x00:                                      break; //NOP        1 4     ----
                case 0x01: setBC(mmu.readWord(PC)); PC += 2;      break; //LD BC,D16  3 12    ----
                case 0x02: mmu.writeByte(getBC(), A);                break; //LD (BC),A  1 8     ----
                case 0x03: setBC((ushort)(0xffff & (getBC() + 1)));                             break; //INC BC     1 8     ----
                case 0x04: B = INC(B);                          break; //INC B      1 4     Z0H-
                case 0x05: B = DEC(B);                          break; //DEC B      1 4     Z1H-
                case 0x06: B = mmu.readByte(PC); PC += 1;       break; //LD B,D8    2 8     ----

                case 0x07: //RLCA 1 4 000C
                    F = 0;
                    setFlagC(((A & 0x80) != 0));
                    A = (byte)(0xff & ((A << 1) | (A >> 7)));
                    break;

                case 0x08: mmu.writeWord(mmu.readWord(PC), SP); PC += 2; break; //LD (A16),SP 3 20   ----
                case 0x09: DAD(getBC());                             break; //ADD HL,BC   1 8    -0HC
                case 0x0A: A = mmu.readByte(getBC());                break; //LD A,(BC)   1 8    ----
                case 0x0B: setBC((ushort)(0xffff & (getBC() - 1)));                             break; //DEC BC      1 8    ----
                case 0x0C: C = INC(C);                          break; //INC C       1 8    Z0H-
                case 0x0D: C = DEC(C);                          break; //DEC C       1 8    Z1H-
                case 0x0E: C = mmu.readByte(PC); PC += 1;       break; //LD C,D8     2 8    ----

                case 0x0F: //RRCA 1 4 000C
                    F = 0;
                    setFlagC(((A & 0x1) != 0));
                    A = (byte)(0xff & ((A >> 1) | (A << 7)));
                    break;

                case 0x10: STOP();                              break; //STOP        2 4    ----
                case 0x11: setDE(mmu.readWord(PC)); PC += 2;      break; //LD DE,D16   3 12   ----
                case 0x12: mmu.writeByte(getDE(), A);                break; //LD (DE),A   1 8    ----
                case 0x13: setDE((ushort)(0xffff & (getDE() + 1))); break; //INC DE      1 8    ----
                case 0x14: D = INC(D);                          break; //INC D       1 8    Z0H-
                case 0x15: D = DEC(D);                          break; //DEC D       1 8    Z1H-
                case 0x16: D = mmu.readByte(PC); PC += 1;       break; //LD D,D8     2 8    ----

                case 0x17://RLA 1 4 000C
                    bool prevC = getFlagC();
                    F = 0;
                    setFlagC((A & 0x80) != 0);
                    A = (byte)(0xff & ((A << 1) | (prevC ? 1 : 0)));
                    break;

                case 0x18: JR(true);                       break; //JR R8       2 12   ----
                case 0x19: DAD(getDE());                             break; //ADD HL,DE   1 8    -0HC
                case 0x1A: A = mmu.readByte(getDE());                break; //LD A,(DE)   1 8    ----
                case 0x1B: setDE((ushort)(0xffff & (getDE() - 1))); break; //INC DE      1 8    ----
                case 0x1C: E = INC(E);                          break; //INC E       1 8    Z0H-
                case 0x1D: E = DEC(E);                          break; //DEC E       1 8    Z1H-
                case 0x1E: E = mmu.readByte(PC); PC += 1;       break; //LD E,D8     2 8    ----

                case 0x1F://RRA 1 4 000C
                    bool preC = getFlagC();
                    F = 0;
                    setFlagC((A & 0x1) != 0);
                    A = (byte)(0xff & ((A >> 1) | (preC ? 0x80 : 0)));
                    break;

                case 0x20: JR(!getFlagZ());                     break; //JR NZ R8    2 12/8 ---- 
                case 0x21: setHL(mmu.readWord(PC)); PC += 2;      break; //LD HL,D16   3 12   ----
                case 0x22: mmu.writeByte(HLIncr(), A);              break; //LD (HL+),A  1 8    ----
                case 0x23: HLIncr();                             break; //INC HL      1 8    ----
                case 0x24: H = INC(H);                          break; //INC H       1 8    Z0H-
                case 0x25: H = DEC(H);                          break; //DEC H       1 8    Z1H-
                case 0x26: H = mmu.readByte(PC); PC += 1; ;     break; //LD H,D8     2 8    ----

                case 0x27: //DAA    1 4 Z-0C
                    if (getFlagN()) { // sub
                        if (getFlagC()) { int newA = A - 0x60; A = (byte)(0xff & newA); }
                        if (getFlagH()) { int newA = A - 0x6; A = (byte)(0xff & newA); }
                    } else { // add
                        if (getFlagC() || (A > 0x99)) { int newA = A + 0x60; A = (byte)(0xff & newA); setFlagC(true); }
                        if (getFlagH() || (A & 0xF) > 0x9) { int newA = A + 0x6; A = (byte)(0xff & newA); }
                    }
                    SetFlagZ(A);
                    setFlagH(false);
                    break;

                case 0x28: JR(getFlagZ());                                 break; //JR Z R8    2 12/8  ----
                case 0x29: DAD(getHL());                                        break; //ADD HL,HL  1 8     -0HC
                case 0x2A: A = mmu.readByte(HLIncr());                         break; //LD A (HL+) 1 8     ----
                case 0x2B: HLDecr();                                     break; //DEC HL     1 4     ----
                case 0x2C: L = INC(L);                                     break; //INC L      1 4     Z0H-
                case 0x2D: L = DEC(L);                                     break; //DEC L      1 4     Z1H-
                case 0x2E: L = mmu.readByte(PC); PC += 1; ;                break; //LD L,D8    2 8     ----
                case 0x2F: A = (byte)(0xff & (~A)); setFlagN(true); setFlagH(true);       break; //CPL	       1 4     -11-

                case 0x30: JR(!getFlagC());                                break; //JR NC R8   2 12/8  ----
                case 0x31: SP = mmu.readWord(PC); PC += 2; ;               break; //LD SP,D16  3 12    ----
                case 0x32: mmu.writeByte(HLDecr(), A);                         break; //LD (HL-),A 1 8     ----
                case 0x33: SP = (ushort)(0xffff & (SP + 1));                                        break; //INC SP     1 8     ----
                case 0x34: mmu.writeByte(getHL(), INC(mmu.readByte(getHL())));       break; //INC (HL)   1 12    Z0H-
                case 0x35: mmu.writeByte(getHL(), DEC(mmu.readByte(getHL())));       break; //DEC (HL)   1 12    Z1H-
                case 0x36: mmu.writeByte(getHL(), mmu.readByte(PC)); PC += 1;   break; //LD (HL),D8 2 12    ----
                case 0x37: setFlagC(true); setFlagN(false); setFlagH(false);     break; //SCF	       1 4     -001

                case 0x38: JR(getFlagC());                                 break; //JR C R8    2 12/8  ----
                case 0x39: DAD(SP);                                        break; //ADD HL,SP  1 8     -0HC
                case 0x3A: A = mmu.readByte(HLDecr());                         break; //LD A (HL-) 1 8     ----
                case 0x3B: SP = (ushort)(0xffff & (SP - 1)); break; //DEC SP     1 8     ----
                case 0x3C: A = INC(A);                                     break; //INC A      1 4     Z0H-
                case 0x3D: A = DEC(A);                                     break; //DEC (HL)   1 4     Z1H-
                case 0x3E: A = mmu.readByte(PC); PC += 1;                  break; //LD A,D8    2 8     ----
                case 0x3F: setFlagC(!getFlagC()); setFlagN (false); setFlagH(false);   break; //CCF        1 4     -00C

                case 0x40: /*B = B;*/             break; //LD B,B	    1 4    ----
                case 0x41: B = C;                 break; //LD B,C	    1 4	   ----
                case 0x42: B = D;                 break; //LD B,D	    1 4	   ----
                case 0x43: B = E;                 break; //LD B,E	    1 4	   ----
                case 0x44: B = H;                 break; //LD B,H	    1 4	   ----
                case 0x45: B = L;                 break; //LD B,L	    1 4	   ----
                case 0x46: B = mmu.readByte(getHL());  break; //LD B,(HL)	1 8	   ----
                case 0x47: B = A;                 break; //LD B,A	    1 4	   ----
                                                 
                case 0x48: C = B;                 break; //LD C,B	    1 4    ----
                case 0x49: /*C = C;*/             break; //LD C,C	    1 4    ----
                case 0x4A: C = D;                 break; //LD C,D   	1 4    ----
                case 0x4B: C = E;                 break; //LD C,E   	1 4    ----
                case 0x4C: C = H;                 break; //LD C,H   	1 4    ----
                case 0x4D: C = L;                 break; //LD C,L   	1 4    ----
                case 0x4E: C = mmu.readByte(getHL());  break; //LD C,(HL)	1 8    ----
                case 0x4F: C = A;                 break; //LD C,A   	1 4    ----
                                                                    
                case 0x50: D = B;                 break; //LD D,B	    1 4    ----
                case 0x51: D = C;                 break; //LD D,C	    1 4    ----
                case 0x52: /*D = D;*/             break; //LD D,D	    1 4    ----
                case 0x53: D = E;                 break; //LD D,E	    1 4    ----
                case 0x54: D = H;                 break; //LD D,H	    1 4    ----
                case 0x55: D = L;                 break; //LD D,L	    1 4    ----
                case 0x56: D = mmu.readByte(getHL());  break; //LD D,(HL)    1 8    ---- 
                case 0x57: D = A;                 break; //LD D,A	    1 4    ----
                                                                    
                case 0x58: E = B;                 break; //LD E,B   	1 4    ----
                case 0x59: E = C;                 break; //LD E,C   	1 4    ----
                case 0x5A: E = D;                 break; //LD E,D   	1 4    ----
                case 0x5B: /*E = E;*/             break; //LD E,E   	1 4    ----
                case 0x5C: E = H;                 break; //LD E,H   	1 4    ----
                case 0x5D: E = L;                 break; //LD E,L   	1 4    ----
                case 0x5E: E = mmu.readByte(getHL());  break; //LD E,(HL)    1 8    ----
                case 0x5F: E = A;                 break; //LD E,A	    1 4    ----
                                                                   
                case 0x60: H = B;                 break; //LD H,B   	1 4    ----
                case 0x61: H = C;                 break; //LD H,C   	1 4    ----
                case 0x62: H = D;                 break; //LD H,D   	1 4    ----
                case 0x63: H = E;                 break; //LD H,E   	1 4    ----
                case 0x64: /*H = H;*/             break; //LD H,H   	1 4    ----
                case 0x65: H = L;                 break; //LD H,L   	1 4    ----
                case 0x66: H = mmu.readByte(getHL());  break; //LD H,(HL)    1 8    ----
                case 0x67: H = A;                 break; //LD H,A	    1 4    ----
                                                                  
                case 0x68: L = B;                 break; //LD L,B   	1 4    ----
                case 0x69: L = C;                 break; //LD L,C   	1 4    ----
                case 0x6A: L = D;                 break; //LD L,D   	1 4    ----
                case 0x6B: L = E;                 break; //LD L,E   	1 4    ----
                case 0x6C: L = H;                 break; //LD L,H   	1 4    ----
                case 0x6D: /*L = L;*/             break; //LD L,L	    1 4    ----
                case 0x6E: L = mmu.readByte(getHL());  break; //LD L,(HL)	1 8    ----
                case 0x6F: L = A;                 break; //LD L,A	    1 4    ----
                                                 
                case 0x70: mmu.writeByte(getHL(), B);  break; //LD (HL),B	1 8    ----
                case 0x71: mmu.writeByte(getHL(), C);  break; //LD (HL),C	1 8	   ----
                case 0x72: mmu.writeByte(getHL(), D);  break; //LD (HL),D	1 8	   ----
                case 0x73: mmu.writeByte(getHL(), E);  break; //LD (HL),E	1 8	   ----
                case 0x74: mmu.writeByte(getHL(), H);  break; //LD (HL),H	1 8	   ----
                case 0x75: mmu.writeByte(getHL(), L);  break; //LD (HL),L	1 8	   ----
                case 0x76: HALT();             break; //HLT	        1 4    ----
                case 0x77: mmu.writeByte(getHL(), A);  break; //LD (HL),A	1 8    ----
                                                 
                case 0x78: A = B;                 break; //LD A,B	    1 4    ----
                case 0x79: A = C;                 break; //LD A,C	    1 4	   ----
                case 0x7A: A = D;                 break; //LD A,D	    1 4	   ----
                case 0x7B: A = E;                 break; //LD A,E	    1 4	   ----
                case 0x7C: A = H;                 break; //LD A,H	    1 4	   ----
                case 0x7D: A = L;                 break; //LD A,L	    1 4	   ----
                case 0x7E: A = mmu.readByte(getHL());  break; //LD A,(HL)    1 8    ----
                case 0x7F: /*A = A;*/             break; //LD A,A	    1 4    ----

                case 0x80: ADD(B);                break; //ADD B	    1 4    Z0HC	
                case 0x81: ADD(C);                break; //ADD C	    1 4    Z0HC	
                case 0x82: ADD(D);                break; //ADD D	    1 4    Z0HC	
                case 0x83: ADD(E);                break; //ADD E	    1 4    Z0HC	
                case 0x84: ADD(H);                break; //ADD H	    1 4    Z0HC	
                case 0x85: ADD(L);                break; //ADD L	    1 4    Z0HC	
                case 0x86: ADD(mmu.readByte(getHL())); break; //ADD M	    1 8    Z0HC	
                case 0x87: ADD(A);                break; //ADD A	    1 4    Z0HC	

                case 0x88: ADC(B);                break; //ADC B	    1 4    Z0HC	
                case 0x89: ADC(C);                break; //ADC C	    1 4    Z0HC	
                case 0x8A: ADC(D);                break; //ADC D	    1 4    Z0HC	
                case 0x8B: ADC(E);                break; //ADC E	    1 4    Z0HC	
                case 0x8C: ADC(H);                break; //ADC H	    1 4    Z0HC	
                case 0x8D: ADC(L);                break; //ADC L	    1 4    Z0HC	
                case 0x8E: ADC(mmu.readByte(getHL())); break; //ADC M	    1 8    Z0HC	
                case 0x8F: ADC(A);                break; //ADC A	    1 4    Z0HC	

                case 0x90: SUB(B);                break; //SUB B	    1 4    Z1HC
                case 0x91: SUB(C);                break; //SUB C	    1 4    Z1HC
                case 0x92: SUB(D);                break; //SUB D	    1 4    Z1HC
                case 0x93: SUB(E);                break; //SUB E	    1 4    Z1HC
                case 0x94: SUB(H);                break; //SUB H	    1 4    Z1HC
                case 0x95: SUB(L);                break; //SUB L	    1 4    Z1HC
                case 0x96: SUB(mmu.readByte(getHL())); break; //SUB M	    1 8    Z1HC
                case 0x97: SUB(A);                break; //SUB A	    1 4    Z1HC

                case 0x98: SBC(B);                break; //SBC B	    1 4    Z1HC
                case 0x99: SBC(C);                break; //SBC C	    1 4    Z1HC
                case 0x9A: SBC(D);                break; //SBC D	    1 4    Z1HC
                case 0x9B: SBC(E);                break; //SBC E	    1 4    Z1HC
                case 0x9C: SBC(H);                break; //SBC H	    1 4    Z1HC
                case 0x9D: SBC(L);                break; //SBC L	    1 4    Z1HC
                case 0x9E: SBC(mmu.readByte(getHL())); break; //SBC M	    1 8    Z1HC
                case 0x9F: SBC(A);                break; //SBC A	    1 4    Z1HC

                case 0xA0: AND(B);                break; //AND B	    1 4    Z010
                case 0xA1: AND(C);                break; //AND C	    1 4    Z010
                case 0xA2: AND(D);                break; //AND D	    1 4    Z010
                case 0xA3: AND(E);                break; //AND E	    1 4    Z010
                case 0xA4: AND(H);                break; //AND H	    1 4    Z010
                case 0xA5: AND(L);                break; //AND L	    1 4    Z010
                case 0xA6: AND(mmu.readByte(getHL())); break; //AND M	    1 8    Z010
                case 0xA7: AND(A);                break; //AND A	    1 4    Z010

                case 0xA8: XOR(B);                break; //XOR B	    1 4    Z000
                case 0xA9: XOR(C);                break; //XOR C	    1 4    Z000
                case 0xAA: XOR(D);                break; //XOR D	    1 4    Z000
                case 0xAB: XOR(E);                break; //XOR E	    1 4    Z000
                case 0xAC: XOR(H);                break; //XOR H	    1 4    Z000
                case 0xAD: XOR(L);                break; //XOR L	    1 4    Z000
                case 0xAE: XOR(mmu.readByte(getHL())); break; //XOR M	    1 8    Z000
                case 0xAF: XOR(A);                break; //XOR A	    1 4    Z000

                case 0xB0: OR(B);                 break; //OR B     	1 4    Z000
                case 0xB1: OR(C);                 break; //OR C     	1 4    Z000
                case 0xB2: OR(D);                 break; //OR D     	1 4    Z000
                case 0xB3: OR(E);                 break; //OR E     	1 4    Z000
                case 0xB4: OR(H);                 break; //OR H     	1 4    Z000
                case 0xB5: OR(L);                 break; //OR L     	1 4    Z000
                case 0xB6: OR(mmu.readByte(getHL()));  break; //OR M     	1 8    Z000
                case 0xB7: OR(A);                 break; //OR A     	1 4    Z000

                case 0xB8: CP(B);                 break; //CP B     	1 4    Z1HC
                case 0xB9: CP(C);                 break; //CP C     	1 4    Z1HC
                case 0xBA: CP(D);                 break; //CP D     	1 4    Z1HC
                case 0xBB: CP(E);                 break; //CP E     	1 4    Z1HC
                case 0xBC: CP(H);                 break; //CP H     	1 4    Z1HC
                case 0xBD: CP(L);                 break; //CP L     	1 4    Z1HC
                case 0xBE: CP(mmu.readByte(getHL()));  break; //CP M     	1 8    Z1HC
                case 0xBF: CP(A);                 break; //CP A     	1 4    Z1HC

                case 0xC0: RETURN(!getFlagZ());             break; //RET NZ	     1 20/8  ----
                case 0xC1: setBC(POP());                   break; //POP BC      1 12    ----
                case 0xC2: JUMP(!getFlagZ());               break; //JP NZ,A16   3 16/12 ----
                case 0xC3: JUMP(true);                 break; //JP A16      3 16    ----
                case 0xC4: CALL(!getFlagZ());               break; //CALL NZ A16 3 24/12 ----
                case 0xC5: PUSH(getBC());                   break; //PUSH BC     1 16    ----
                case 0xC6: ADD(mmu.readByte(PC)); PC += 1;  break; //ADD A,D8    2 8     Z0HC
                case 0xC7: RST(0x0);                   break; //RST 0       1 16    ----

                case 0xC8: RETURN(getFlagZ());              break; //RET Z       1 20/8  ----
                case 0xC9: RETURN(true);               break; //RET         1 16    ----
                case 0xCA: JUMP(getFlagZ());                break; //JP Z,A16    3 16/12 ----
                case 0xCB: PREFIX_CB(mmu.readByte(PC++));      break; //PREFIX CB OPCODE TABLE
                case 0xCC: CALL(getFlagZ());                break; //CALL Z,A16  3 24/12 ----
                case 0xCD: CALL(true);                 break; //CALL A16    3 24    ----
                case 0xCE: ADC(mmu.readByte(PC)); PC += 1;  break; //ADC A,D8    2 8     ----
                case 0xCF: RST(0x8);                   break; //RST 1 08    1 16    ----

                case 0xD0: RETURN(!getFlagC());             break; //RET NC      1 20/8  ----
                case 0xD1: setDE(POP());                   break; //POP DE      1 12    ----
                case 0xD2: JUMP(!getFlagC());               break; //JP NC,A16   3 16/12 ----
                //case 0xD3:                                break; //Illegal Opcode
                case 0xD4: CALL(!getFlagC());               break; //CALL NC,A16 3 24/12 ----
                case 0xD5: PUSH(getDE());                   break; //PUSH DE     1 16    ----
                case 0xD6: SUB(mmu.readByte(PC)); PC += 1;  break; //SUB D8      2 8     ----
                case 0xD7: RST(0x10);                  break; //RST 2 10    1 16    ----

                case 0xD8: RETURN(getFlagC());              break; //RET C       1 20/8  ----
                case 0xD9: RETURN(true); IME = true;   break; //RETI        1 16    ----
                case 0xDA: JUMP(getFlagC());                break; //JP C,A16    3 16/12 ----
                //case 0xDB:                                break; //Illegal Opcode
                case 0xDC: CALL(getFlagC());                break; //Call C,A16  3 24/12 ----
                //case 0xDD:                                break; //Illegal Opcode
                case 0xDE: SBC(mmu.readByte(PC)); PC += 1;  break; //SBC A,A8    2 8     Z1HC
                case 0xDF: RST(0x18);                  break; //RST 3 18    1 16    ----

                case 0xE0: mmu.writeByte((ushort)(0xFF00 + mmu.readByte(PC)), A); PC += 1;  break; //LDH (A8),A 2 12 ----
                case 0xE1: setHL(POP());                   break; //POP HL      1 12    ----
                case 0xE2: mmu.writeByte((ushort)(0xFF00 + C), A);   break; //LD (C),A   1 8  ----
                //case 0xE3:                                break; //Illegal Opcode
                //case 0xE4:                                break; //Illegal Opcode
                case 0xE5: PUSH(getHL());                   break; //PUSH HL     1 16    ----
                case 0xE6: AND(mmu.readByte(PC)); PC += 1;  break; //AND D8      2 8     Z010
                case 0xE7: RST(0x20);                  break; //RST 4 20    1 16    ----

                case 0xE8: SP = DADr8(SP);             break; //ADD SP,R8   2 16    00HC
                case 0xE9: PC = getHL();                         break; //JP (HL)     1 4     ----
                case 0xEA: mmu.writeByte(mmu.readWord(PC), A); PC += 2;                     break; //LD (A16),A 3 16 ----
                //case 0xEB:                                break; //Illegal Opcode
                //case 0xEC:                                break; //Illegal Opcode
                //case 0xED:                                break; //Illegal Opcode
                case 0xEE: XOR(mmu.readByte(PC)); PC += 1;  break; //XOR D8      2 8     Z000
                case 0xEF: RST(0x28);                  break; //RST 5 28    1 16    ----

                case 0xF0: A = mmu.readByte((ushort)(0xFF00 + mmu.readByte(PC))); PC += 1;  break; //LDH A,(A8)  2 12    ----
                case 0xF1: setAF(POP());                   break; //POP AF      1 12    ZNHC
                case 0xF2: A = mmu.readByte((ushort)(0xFF00 + C));  break; //LD A,(C)    1 8     ----
                case 0xF3: IME = false;                     break; //DI          1 4     ----
                //case 0xF4:                                break; //Illegal Opcode
                case 0xF5: PUSH(getAF());                   break; //PUSH AF     1 16    ----
                case 0xF6: OR(mmu.readByte(PC)); PC += 1;   break; //OR D8       2 8     Z000
                case 0xF7: RST(0x30);                  break; //RST 6 30    1 16    ----

                case 0xF8: setHL(DADr8(SP));             break; //LD HL,SP+R8 2 12    00HC
                case 0xF9: SP = getHL();                         break; //LD SP,HL    1 8     ----
                case 0xFA: A = mmu.readByte(mmu.readWord(PC)); PC += 2;   break; //LD A,(A16)  3 16    ----
                case 0xFB: IMEEnabler = true;               break; //IE          1 4     ----
                //case 0xFC:                                break; //Illegal Opcode
                //case 0xFD:                                break; //Illegal Opcode
                case 0xFE: CP(mmu.readByte(PC)); PC += 1;   break; //CP D8       2 8     Z1HC
                case 0xFF: RST(0x38);                  break; //RST 7 38    1 16    ----

                default: warnUnsupportedOpcode(opcode);     break;
            }
            cycles += cyclesUtil.Value[opcode];
            return cycles;
        }

        private void PREFIX_CB(byte opcode) {
            switch (opcode) {
                case 0x00: B = RLC(B);                                  break; //RLC B    2   8   Z00C
                case 0x01: C = RLC(C);                                  break; //RLC C    2   8   Z00C
                case 0x02: D = RLC(D);                                  break; //RLC D    2   8   Z00C
                case 0x03: E = RLC(E);                                  break; //RLC E    2   8   Z00C
                case 0x04: H = RLC(H);                                  break; //RLC H    2   8   Z00C
                case 0x05: L = RLC(L);                                  break; //RLC L    2   8   Z00C
                case 0x06: mmu.writeByte(getHL(), RLC(mmu.readByte(getHL())));    break; //RLC (HL) 2   8   Z00C
                case 0x07: A = RLC(A);                                  break; //RLC B    2   8   Z00C
                                                                        
                case 0x08: B = RRC(B);                                  break; //RRC B    2   8   Z00C
                case 0x09: C = RRC(C);                                  break; //RRC C    2   8   Z00C
                case 0x0A: D = RRC(D);                                  break; //RRC D    2   8   Z00C
                case 0x0B: E = RRC(E);                                  break; //RRC E    2   8   Z00C
                case 0x0C: H = RRC(H);                                  break; //RRC H    2   8   Z00C
                case 0x0D: L = RRC(L);                                  break; //RRC L    2   8   Z00C
                case 0x0E: mmu.writeByte(getHL(), RRC(mmu.readByte(getHL())));    break; //RRC (HL) 2   8   Z00C
                case 0x0F: A = RRC(A);                                  break; //RRC B    2   8   Z00C
                                                                           
                case 0x10: B = RL(B);                                   break; //RL B     2   8   Z00C
                case 0x11: C = RL(C);                                   break; //RL C     2   8   Z00C
                case 0x12: D = RL(D);                                   break; //RL D     2   8   Z00C
                case 0x13: E = RL(E);                                   break; //RL E     2   8   Z00C
                case 0x14: H = RL(H);                                   break; //RL H     2   8   Z00C
                case 0x15: L = RL(L);                                   break; //RL L     2   8   Z00C
                case 0x16: mmu.writeByte(getHL(), RL(mmu.readByte(getHL())));     break; //RL (HL)  2   8   Z00C
                case 0x17: A = RL(A);                                   break; //RL B     2   8   Z00C
                                                                                             
                case 0x18: B = RR(B);                                   break; //RR B     2   8   Z00C
                case 0x19: C = RR(C);                                   break; //RR C     2   8   Z00C
                case 0x1A: D = RR(D);                                   break; //RR D     2   8   Z00C
                case 0x1B: E = RR(E);                                   break; //RR E     2   8   Z00C
                case 0x1C: H = RR(H);                                   break; //RR H     2   8   Z00C
                case 0x1D: L = RR(L);                                   break; //RR L     2   8   Z00C
                case 0x1E: mmu.writeByte(getHL(), RR(mmu.readByte(getHL())));     break; //RR (HL)  2   8   Z00C
                case 0x1F: A = RR(A);                                   break; //RR B     2   8   Z00C
                                                                           
                case 0x20: B = SLA(B);                                  break; //SLA B    2   8   Z00C
                case 0x21: C = SLA(C);                                  break; //SLA C    2   8   Z00C
                case 0x22: D = SLA(D);                                  break; //SLA D    2   8   Z00C
                case 0x23: E = SLA(E);                                  break; //SLA E    2   8   Z00C
                case 0x24: H = SLA(H);                                  break; //SLA H    2   8   Z00C
                case 0x25: L = SLA(L);                                  break; //SLA L    2   8   Z00C
                case 0x26: mmu.writeByte(getHL(), SLA(mmu.readByte(getHL())));    break; //SLA (HL) 2   8   Z00C
                case 0x27: A = SLA(A);                                  break; //SLA B    2   8   Z00C
                                                                           
                case 0x28: B = SRA(B);                                  break; //SRA B    2   8   Z00C
                case 0x29: C = SRA(C);                                  break; //SRA C    2   8   Z00C
                case 0x2A: D = SRA(D);                                  break; //SRA D    2   8   Z00C
                case 0x2B: E = SRA(E);                                  break; //SRA E    2   8   Z00C
                case 0x2C: H = SRA(H);                                  break; //SRA H    2   8   Z00C
                case 0x2D: L = SRA(L);                                  break; //SRA L    2   8   Z00C
                case 0x2E: mmu.writeByte(getHL(), SRA(mmu.readByte(getHL())));    break; //SRA (HL) 2   8   Z00C
                case 0x2F: A = SRA(A);                                  break; //SRA B    2   8   Z00C
                                                                          
                case 0x30: B = SWAP(B);                                 break; //SWAP B    2   8   Z00C
                case 0x31: C = SWAP(C);                                 break; //SWAP C    2   8   Z00C
                case 0x32: D = SWAP(D);                                 break; //SWAP D    2   8   Z00C
                case 0x33: E = SWAP(E);                                 break; //SWAP E    2   8   Z00C
                case 0x34: H = SWAP(H);                                 break; //SWAP H    2   8   Z00C
                case 0x35: L = SWAP(L);                                 break; //SWAP L    2   8   Z00C
                case 0x36: mmu.writeByte(getHL(), SWAP(mmu.readByte(getHL())));   break; //SWAP (HL) 2   8   Z00C
                case 0x37: A = SWAP(A);                                 break; //SWAP B    2   8   Z00C
                                                                          
                case 0x38: B = SRL(B);                                  break; //SRL B    2   8   Z000
                case 0x39: C = SRL(C);                                  break; //SRL C    2   8   Z000
                case 0x3A: D = SRL(D);                                  break; //SRL D    2   8   Z000
                case 0x3B: E = SRL(E);                                  break; //SRL E    2   8   Z000
                case 0x3C: H = SRL(H);                                  break; //SRL H    2   8   Z000
                case 0x3D: L = SRL(L);                                  break; //SRL L    2   8   Z000
                case 0x3E: mmu.writeByte(getHL(), SRL(mmu.readByte(getHL())));    break; //SRL (HL) 2   8   Z000
                case 0x3F: A = SRL(A);                                  break; //SRL B    2   8   Z000

                case 0x40: BIT(0x1, B);                                 break; //BIT B    2   8   Z01-
                case 0x41: BIT(0x1, C);                                 break; //BIT C    2   8   Z01-
                case 0x42: BIT(0x1, D);                                 break; //BIT D    2   8   Z01-
                case 0x43: BIT(0x1, E);                                 break; //BIT E    2   8   Z01-
                case 0x44: BIT(0x1, H);                                 break; //BIT H    2   8   Z01-
                case 0x45: BIT(0x1, L);                                 break; //BIT L    2   8   Z01-
                case 0x46: BIT(0x1, mmu.readByte(getHL()));                  break; //BIT (HL) 2   8   Z01-
                case 0x47: BIT(0x1, A);                                 break; //BIT B    2   8   Z01-

                case 0x48: BIT(0x2, B);                                break; //BIT B    2   8   Z01-
                case 0x49: BIT(0x2, C);                                break; //BIT C    2   8   Z01-
                case 0x4A: BIT(0x2, D);                                break; //BIT D    2   8   Z01-
                case 0x4B: BIT(0x2, E);                                break; //BIT E    2   8   Z01-
                case 0x4C: BIT(0x2, H);                                break; //BIT H    2   8   Z01-
                case 0x4D: BIT(0x2, L);                                break; //BIT L    2   8   Z01-
                case 0x4E: BIT(0x2, mmu.readByte(getHL()));                 break; //BIT (HL) 2   8   Z01-
                case 0x4F: BIT(0x2, A);                                break; //BIT B    2   8   Z01-
                                                                       
                case 0x50: BIT(0x4, B);                                break; //BIT B    2   8   Z01-
                case 0x51: BIT(0x4, C);                                break; //BIT C    2   8   Z01-
                case 0x52: BIT(0x4, D);                                break; //BIT D    2   8   Z01-
                case 0x53: BIT(0x4, E);                                break; //BIT E    2   8   Z01-
                case 0x54: BIT(0x4, H);                                break; //BIT H    2   8   Z01-
                case 0x55: BIT(0x4, L);                                break; //BIT L    2   8   Z01-
                case 0x56: BIT(0x4, mmu.readByte(getHL()));                 break; //BIT (HL) 2   8   Z01-
                case 0x57: BIT(0x4, A);                                break; //BIT B    2   8   Z01-

                case 0x58: BIT(0x8, B);                                break; //BIT B    2   8   Z01-
                case 0x59: BIT(0x8, C);                                break; //BIT C    2   8   Z01-
                case 0x5A: BIT(0x8, D);                                break; //BIT D    2   8   Z01-
                case 0x5B: BIT(0x8, E);                                break; //BIT E    2   8   Z01-
                case 0x5C: BIT(0x8, H);                                break; //BIT H    2   8   Z01-
                case 0x5D: BIT(0x8, L);                                break; //BIT L    2   8   Z01-
                case 0x5E: BIT(0x8, mmu.readByte(getHL()));                 break; //BIT (HL) 2   8   Z01-
                case 0x5F: BIT(0x8, A);                                break; //BIT B    2   8   Z01-

                case 0x60: BIT(0x10, B);                               break; //BIT B    2   8   Z01-
                case 0x61: BIT(0x10, C);                               break; //BIT C    2   8   Z01-
                case 0x62: BIT(0x10, D);                               break; //BIT D    2   8   Z01-
                case 0x63: BIT(0x10, E);                               break; //BIT E    2   8   Z01-
                case 0x64: BIT(0x10, H);                               break; //BIT H    2   8   Z01-
                case 0x65: BIT(0x10, L);                               break; //BIT L    2   8   Z01-
                case 0x66: BIT(0x10, mmu.readByte(getHL()));                break; //BIT (HL) 2   8   Z01-
                case 0x67: BIT(0x10, A);                               break; //BIT B    2   8   Z01-

                case 0x68: BIT(0x20, B);                               break; //BIT B    2   8   Z01-
                case 0x69: BIT(0x20, C);                               break; //BIT C    2   8   Z01-
                case 0x6A: BIT(0x20, D);                               break; //BIT D    2   8   Z01-
                case 0x6B: BIT(0x20, E);                               break; //BIT E    2   8   Z01-
                case 0x6C: BIT(0x20, H);                               break; //BIT H    2   8   Z01-
                case 0x6D: BIT(0x20, L);                               break; //BIT L    2   8   Z01-
                case 0x6E: BIT(0x20, mmu.readByte(getHL()));                break; //BIT (HL) 2   8   Z01-
                case 0x6F: BIT(0x20, A);                               break; //BIT B    2   8   Z01-

                case 0x70: BIT(0x40, B);                               break; //BIT B    2   8   Z01-
                case 0x71: BIT(0x40, C);                               break; //BIT C    2   8   Z01-
                case 0x72: BIT(0x40, D);                               break; //BIT D    2   8   Z01-
                case 0x73: BIT(0x40, E);                               break; //BIT E    2   8   Z01-
                case 0x74: BIT(0x40, H);                               break; //BIT H    2   8   Z01-
                case 0x75: BIT(0x40, L);                               break; //BIT L    2   8   Z01-
                case 0x76: BIT(0x40, mmu.readByte(getHL()));                break; //BIT (HL) 2   8   Z01-
                case 0x77: BIT(0x40, A);                               break; //BIT B    2   8   Z01-

                case 0x78: BIT(0x80, B);                               break; //BIT B    2   8   Z01-
                case 0x79: BIT(0x80, C);                               break; //BIT C    2   8   Z01-
                case 0x7A: BIT(0x80, D);                               break; //BIT D    2   8   Z01-
                case 0x7B: BIT(0x80, E);                               break; //BIT E    2   8   Z01-
                case 0x7C: BIT(0x80, H);                               break; //BIT H    2   8   Z01-
                case 0x7D: BIT(0x80, L);                               break; //BIT L    2   8   Z01-
                case 0x7E: BIT(0x80, mmu.readByte(getHL()));                break; //BIT (HL) 2   8   Z01-
                case 0x7F: BIT(0x80, A);                               break; //BIT B    2   8   Z01-

                case 0x80: B = RES(0x1, B);                               break; //RES B    2   8   ----
                case 0x81: C = RES(0x1, C);                               break; //RES C    2   8   ----
                case 0x82: D = RES(0x1, D);                               break; //RES D    2   8   ----
                case 0x83: E = RES(0x1, E);                               break; //RES E    2   8   ----
                case 0x84: H = RES(0x1, H);                               break; //RES H    2   8   ----
                case 0x85: L = RES(0x1, L);                               break; //RES L    2   8   ----
                case 0x86: mmu.writeByte(getHL(), RES(0x1, mmu.readByte(getHL()))); break; //RES (HL) 2   8   ----
                case 0x87: A = RES(0x1, A);                               break; //RES B    2   8   ----

                case 0x88: B = RES(0x2, B);                               break; //RES B    2   8   ----
                case 0x89: C = RES(0x2, C);                               break; //RES C    2   8   ----
                case 0x8A: D = RES(0x2, D);                               break; //RES D    2   8   ----
                case 0x8B: E = RES(0x2, E);                               break; //RES E    2   8   ----
                case 0x8C: H = RES(0x2, H);                               break; //RES H    2   8   ----
                case 0x8D: L = RES(0x2, L);                               break; //RES L    2   8   ----
                case 0x8E: mmu.writeByte(getHL(), RES(0x2, mmu.readByte(getHL()))); break; //RES (HL) 2   8   ----
                case 0x8F: A = RES(0x2, A);                               break; //RES B    2   8   ----

                case 0x90: B = RES(0x4, B);                               break; //RES B    2   8   ----
                case 0x91: C = RES(0x4, C);                               break; //RES C    2   8   ----
                case 0x92: D = RES(0x4, D);                               break; //RES D    2   8   ----
                case 0x93: E = RES(0x4, E);                               break; //RES E    2   8   ----
                case 0x94: H = RES(0x4, H);                               break; //RES H    2   8   ----
                case 0x95: L = RES(0x4, L);                               break; //RES L    2   8   ----
                case 0x96: mmu.writeByte(getHL(), RES(0x4, mmu.readByte(getHL()))); break; //RES (HL) 2   8   ----
                case 0x97: A = RES(0x4, A);                               break; //RES B    2   8   ----

                case 0x98: B = RES(0x8, B);                               break; //RES B    2   8   ----
                case 0x99: C = RES(0x8, C);                               break; //RES C    2   8   ----
                case 0x9A: D = RES(0x8, D);                               break; //RES D    2   8   ----
                case 0x9B: E = RES(0x8, E);                               break; //RES E    2   8   ----
                case 0x9C: H = RES(0x8, H);                               break; //RES H    2   8   ----
                case 0x9D: L = RES(0x8, L);                               break; //RES L    2   8   ----
                case 0x9E: mmu.writeByte(getHL(), RES(0x8, mmu.readByte(getHL()))); break; //RES (HL) 2   8   ----
                case 0x9F: A = RES(0x8, A);                               break; //RES B    2   8   ----

                case 0xA0: B = RES(0x10, B);                               break; //RES B    2   8   ----
                case 0xA1: C = RES(0x10, C);                               break; //RES C    2   8   ----
                case 0xA2: D = RES(0x10, D);                               break; //RES D    2   8   ----
                case 0xA3: E = RES(0x10, E);                               break; //RES E    2   8   ----
                case 0xA4: H = RES(0x10, H);                               break; //RES H    2   8   ----
                case 0xA5: L = RES(0x10, L);                               break; //RES L    2   8   ----
                case 0xA6: mmu.writeByte(getHL(), RES(0x10, mmu.readByte(getHL()))); break; //RES (HL) 2   8   ----
                case 0xA7: A = RES(0x10, A);                               break; //RES B    2   8   ----

                case 0xA8: B = RES(0x20, B);                               break; //RES B    2   8   ----
                case 0xA9: C = RES(0x20, C);                               break; //RES C    2   8   ----
                case 0xAA: D = RES(0x20, D);                               break; //RES D    2   8   ----
                case 0xAB: E = RES(0x20, E);                               break; //RES E    2   8   ----
                case 0xAC: H = RES(0x20, H);                               break; //RES H    2   8   ----
                case 0xAD: L = RES(0x20, L);                               break; //RES L    2   8   ----
                case 0xAE: mmu.writeByte(getHL(), RES(0x20, mmu.readByte(getHL()))); break; //RES (HL) 2   8   ----
                case 0xAF: A = RES(0x20, A);                               break; //RES B    2   8   ----

                case 0xB0: B = RES(0x40, B);                               break; //RES B    2   8   ----
                case 0xB1: C = RES(0x40, C);                               break; //RES C    2   8   ----
                case 0xB2: D = RES(0x40, D);                               break; //RES D    2   8   ----
                case 0xB3: E = RES(0x40, E);                               break; //RES E    2   8   ----
                case 0xB4: H = RES(0x40, H);                               break; //RES H    2   8   ----
                case 0xB5: L = RES(0x40, L);                               break; //RES L    2   8   ----
                case 0xB6: mmu.writeByte(getHL(), RES(0x40, mmu.readByte(getHL()))); break; //RES (HL) 2   8   ----
                case 0xB7: A = RES(0x40, A);                               break; //RES B    2   8   ----

                case 0xB8: B = RES(0x80, B);                               break; //RES B    2   8   ----
                case 0xB9: C = RES(0x80, C);                               break; //RES C    2   8   ----
                case 0xBA: D = RES(0x80, D);                               break; //RES D    2   8   ----
                case 0xBB: E = RES(0x80, E);                               break; //RES E    2   8   ----
                case 0xBC: H = RES(0x80, H);                               break; //RES H    2   8   ----
                case 0xBD: L = RES(0x80, L);                               break; //RES L    2   8   ----
                case 0xBE: mmu.writeByte(getHL(), RES(0x80, mmu.readByte(getHL()))); break; //RES (HL) 2   8   ----
                case 0xBF: A = RES(0x80, A);                               break; //RES B    2   8   ----

                case 0xC0: B = SET(0x1, B);                               break; //SET B    2   8   ----
                case 0xC1: C = SET(0x1, C);                               break; //SET C    2   8   ----
                case 0xC2: D = SET(0x1, D);                               break; //SET D    2   8   ----
                case 0xC3: E = SET(0x1, E);                               break; //SET E    2   8   ----
                case 0xC4: H = SET(0x1, H);                               break; //SET H    2   8   ----
                case 0xC5: L = SET(0x1, L);                               break; //SET L    2   8   ----
                case 0xC6: mmu.writeByte(getHL(), SET(0x1, mmu.readByte(getHL()))); break; //SET (HL) 2   8   ----
                case 0xC7: A = SET(0x1, A);                               break; //SET B    2   8   ----

                case 0xC8: B = SET(0x2, B);                               break; //SET B    2   8   ----
                case 0xC9: C = SET(0x2, C);                               break; //SET C    2   8   ----
                case 0xCA: D = SET(0x2, D);                               break; //SET D    2   8   ----
                case 0xCB: E = SET(0x2, E);                               break; //SET E    2   8   ----
                case 0xCC: H = SET(0x2, H);                               break; //SET H    2   8   ----
                case 0xCD: L = SET(0x2, L);                               break; //SET L    2   8   ----
                case 0xCE: mmu.writeByte(getHL(), SET(0x2, mmu.readByte(getHL()))); break; //SET (HL) 2   8   ----
                case 0xCF: A = SET(0x2, A);                               break; //SET B    2   8   ----

                case 0xD0: B = SET(0x4, B);                               break; //SET B    2   8   ----
                case 0xD1: C = SET(0x4, C);                               break; //SET C    2   8   ----
                case 0xD2: D = SET(0x4, D);                               break; //SET D    2   8   ----
                case 0xD3: E = SET(0x4, E);                               break; //SET E    2   8   ----
                case 0xD4: H = SET(0x4, H);                               break; //SET H    2   8   ----
                case 0xD5: L = SET(0x4, L);                               break; //SET L    2   8   ----
                case 0xD6: mmu.writeByte(getHL(), SET(0x4, mmu.readByte(getHL()))); break; //SET (HL) 2   8   ----
                case 0xD7: A = SET(0x4, A);                               break; //SET B    2   8   ----

                case 0xD8: B = SET(0x8, B);                               break; //SET B    2   8   ----
                case 0xD9: C = SET(0x8, C);                               break; //SET C    2   8   ----
                case 0xDA: D = SET(0x8, D);                               break; //SET D    2   8   ----
                case 0xDB: E = SET(0x8, E);                               break; //SET E    2   8   ----
                case 0xDC: H = SET(0x8, H);                               break; //SET H    2   8   ----
                case 0xDD: L = SET(0x8, L);                               break; //SET L    2   8   ----
                case 0xDE: mmu.writeByte(getHL(), SET(0x8, mmu.readByte(getHL()))); break; //SET (HL) 2   8   ----
                case 0xDF: A = SET(0x8, A);                               break; //SET B    2   8   ----

                case 0xE0: B = SET(0x10, B);                               break; //SET B    2   8   ----
                case 0xE1: C = SET(0x10, C);                               break; //SET C    2   8   ----
                case 0xE2: D = SET(0x10, D);                               break; //SET D    2   8   ----
                case 0xE3: E = SET(0x10, E);                               break; //SET E    2   8   ----
                case 0xE4: H = SET(0x10, H);                               break; //SET H    2   8   ----
                case 0xE5: L = SET(0x10, L);                               break; //SET L    2   8   ----
                case 0xE6: mmu.writeByte(getHL(), SET(0x10, mmu.readByte(getHL()))); break; //SET (HL) 2   8   ----
                case 0xE7: A = SET(0x10, A);                               break; //SET B    2   8   ----

                case 0xE8: B = SET(0x20, B);                               break; //SET B    2   8   ----
                case 0xE9: C = SET(0x20, C);                               break; //SET C    2   8   ----
                case 0xEA: D = SET(0x20, D);                               break; //SET D    2   8   ----
                case 0xEB: E = SET(0x20, E);                               break; //SET E    2   8   ----
                case 0xEC: H = SET(0x20, H);                               break; //SET H    2   8   ----
                case 0xED: L = SET(0x20, L);                               break; //SET L    2   8   ----
                case 0xEE: mmu.writeByte(getHL(), SET(0x20, mmu.readByte(getHL()))); break; //SET (HL) 2   8   ----
                case 0xEF: A = SET(0x20, A);                               break; //SET B    2   8   ----

                case 0xF0: B = SET(0x40, B);                               break; //SET B    2   8   ----
                case 0xF1: C = SET(0x40, C);                               break; //SET C    2   8   ----
                case 0xF2: D = SET(0x40, D);                               break; //SET D    2   8   ----
                case 0xF3: E = SET(0x40, E);                               break; //SET E    2   8   ----
                case 0xF4: H = SET(0x40, H);                               break; //SET H    2   8   ----
                case 0xF5: L = SET(0x40, L);                               break; //SET L    2   8   ----
                case 0xF6: mmu.writeByte(getHL(), SET(0x40, mmu.readByte(getHL()))); break; //SET (HL) 2   8   ----
                case 0xF7: A = SET(0x40, A);                               break; //SET B    2   8   ----

                case 0xF8: B = SET(0x80, B);                               break; //SET B    2   8   ----
                case 0xF9: C = SET(0x80, C);                               break; //SET C    2   8   ----
                case 0xFA: D = SET(0x80, D);                               break; //SET D    2   8   ----
                case 0xFB: E = SET(0x80, E);                               break; //SET E    2   8   ----
                case 0xFC: H = SET(0x80, H);                               break; //SET H    2   8   ----
                case 0xFD: L = SET(0x80, L);                               break; //SET L    2   8   ----
                case 0xFE: mmu.writeByte(getHL(), SET(0x80, mmu.readByte(getHL()))); break; //SET (HL) 2   8   ----
                case 0xFF: A = SET(0x80, A);                               break; //SET B    2   8   ----

                default: warnUnsupportedOpcode(opcode); break;
            }
            cycles += cyclesUtil.CBValue[opcode];
        }

        private byte SET(byte b, byte reg) {//----
            return (byte)(reg | b);
        }

        private byte RES(int b, byte reg) {//----
            return (byte)(reg & ~b);
        }

        private void BIT(byte b, byte reg) {//Z01-
            setFlagZ((reg & b) == 0);
            setFlagN(false);
            setFlagH(true);
        }

        private byte SRL(byte b) {//Z00C
            byte result = (byte)(0xff & (b >> 1));
            SetFlagZ(result);
            setFlagN(false);
            setFlagH(false);
            setFlagC((b & 0x1) != 0);
            return result;
        }

        private byte SWAP(byte b) {//Z000
            byte result = (byte)(0xff & ((b & 0xF0) >> 4 | (b & 0x0F) << 4));
            SetFlagZ(result);
            setFlagN(false);
            setFlagH(false);
            setFlagC(false);
            return result;
        }

        private byte SRA(byte b) {//Z00C
            byte result = (byte)(0xff & ((b >> 1) | ( b & 0x80)));
            SetFlagZ(result);
            setFlagN(false);
            setFlagH(false);
            setFlagC((b & 0x1) != 0);
            return result;
        }

        private byte SLA(byte b) {//Z00C
            byte result = (byte)(0xff & (b << 1));
            SetFlagZ(result);
            setFlagN(false);
            setFlagH(false);
            setFlagC((b & 0x80) != 0);
            return result;
        }

        private byte RR(byte b) {//Z00C
            bool prevC = getFlagC();
            byte result = (byte)(0xff & ((b >> 1) | (prevC ? 0x80 : 0)));
            SetFlagZ(result);
            setFlagN(false);
            setFlagH(false);
            setFlagC((b & 0x1) != 0);
            return result;
        }

        private byte RL(byte b) {//Z00C
            bool prevC = getFlagC();
            byte result = (byte)(0xff & ((b << 1) | (prevC ? 1 : 0)));
            SetFlagZ(result);
            setFlagN(false);
            setFlagH(false);
            setFlagC((b & 0x80) != 0);
            return result;
        }

        private byte RRC(byte b) {//Z00C
            byte result = (byte)(0xff & ((b >> 1) | (b << 7)));
            SetFlagZ(result);
            setFlagN(false);
            setFlagH(false);
            setFlagC((b & 0x1) != 0);
            return result;
        }

        private byte RLC(byte b) {//Z00C
            byte result = (byte)(0xff & ((b << 1) | (b >> 7)));
            SetFlagZ(result);
            setFlagN(false);
            setFlagH(false);
            setFlagC((b & 0x80) != 0);
            return result;
        }

        private ushort DADr8(ushort w) {//00HC | warning r8 is signed!
            byte b = mmu.readByte(PC++);
            setFlagZ(false);
            setFlagN(false);
            SetFlagH((byte)(0xff & w), b);
            SetFlagC((0xff & w) + b);
            return (ushort)(0xffff & (w + toSByte(b)));
    }
        public sbyte toSByte(byte b)
        {
            if (b <= 127)
            {
                return (sbyte)b;
            }
            else
            {
                return (sbyte)(-(256 - b));
            }
        }

    private void JR(bool flag) {
            if (flag) {
                sbyte sb = toSByte(mmu.readByte(PC));
                PC = (ushort)(PC + sb);
                PC += 1; //<---- //TODO WHAT?
                cycles += cyclesUtil.JUMP_RELATIVE_TRUE;
            } else {
                PC += 1;
                cycles += cyclesUtil.JUMP_RELATIVE_FALSE;
            }
        }

        private void STOP() {
        Debug.Log("called stop, not impld");
        }

        private byte INC(byte b) { //Z0H-
            int result = b + 1;
            SetFlagZ(result);
            setFlagN(false);
            SetFlagH(b, 1);
            return (byte)(0xff & result);
        }

        private byte DEC(byte b) { //Z1H-
            int result = b - 1;
            SetFlagZ(result);
            setFlagN(true);
            SetFlagHSub(b, 1);
            return (byte)(0xff & result);
        }

        private void ADD(byte b) { //Z0HC
            int result = A + b;
            SetFlagZ(result);
            setFlagN(false);
            SetFlagH(A, b);
            SetFlagC(result);
            A = (byte)(0xff & result);
        }

        private void ADC(byte b) { //Z0HC
            int carry = getFlagC() ? 1 : 0;
            int result = A + b + carry;
            SetFlagZ(result);
            setFlagN(false);
            if (getFlagC())
                SetFlagHCarry(A, b);
            else SetFlagH(A, b);
            SetFlagC(result);
            A = (byte)(0xff & result);
        }

        private void SUB(byte b) {//Z1HC
            int result = A - b;
            SetFlagZ(result);
            setFlagN(true);
            SetFlagHSub(A, b);
            SetFlagC(result);
            A = (byte)(0xff & result);
        }

        private void SBC(byte b) {//Z1HC
            int carry = getFlagC() ? 1 : 0;
            int result = A - b - carry;
            SetFlagZ(result);
            setFlagN(true);
            if (getFlagC())
                SetFlagHSubCarry(A, b);
            else SetFlagHSub(A, b);
            SetFlagC(result);
            A = (byte)(0xff & result);
        }

        private void AND(byte b) {//Z010
            byte result = (byte)(A & b);
            SetFlagZ(result);
            setFlagN(false);
            setFlagH(true);
            setFlagC(false);
            A = result;
        }

        private void XOR(byte b) {//Z000
            byte result = (byte)(A ^ b);
            SetFlagZ(result);
            setFlagN(false);
            setFlagH(false);
            setFlagC(false);
            A = result;
        }

        private void OR(byte b) {//Z000
            byte result = (byte)(A | b);
            SetFlagZ(result);
            setFlagN(false);
            setFlagH(false);
            setFlagC(false);
            A = result;
        }

        private void CP(byte b) {//Z1HC
            int result = A - b;
            SetFlagZ(result);
            setFlagN(true);
            SetFlagHSub(A, b);
            SetFlagC(result);
        }

        private void DAD(ushort w) { //-0HC
            int result = getHL() + w;
            setFlagN(false);
            SetFlagH(getHL(), w); //Special Flag H with word
            setFlagC(result >> 16 != 0); //Special FlagC as short value involved
            setHL((ushort)(0xffff & result));
        }

        private void RETURN(bool flag) {
            if (flag) {
                PC = POP();
                cycles += cyclesUtil.RETURN_TRUE;
            } else {
                cycles += cyclesUtil.RETURN_FALSE;
            }
        }

        private void CALL(bool flag) {
            if (flag) {
                PUSH((ushort)(PC + 2));
                PC = mmu.readWord(PC);
                cycles += cyclesUtil.CALL_TRUE;
            } else {
                PC += 2;
                cycles += cyclesUtil.CALL_FALSE;
            }
        }

        private void JUMP(bool flag) {
            if (flag) {
                PC = mmu.readWord(PC);
                cycles += cyclesUtil.JUMP_TRUE;
            } else {
                PC += 2;
                cycles += cyclesUtil.JUMP_FALSE;
            }
        }

        private void RST(byte b) {
            PUSH(PC);
            PC = b;
        }


        private void HALT() {
            if (!IME) {
                if ((mmu.IE() & mmu.IF() & 0x1F) == 0) {
                    HALTED = true;
                    PC--;
                } else {
                    HALT_BUG = true;
                }
            }
        }

        public void UpdateIME() {
            IME |= IMEEnabler;
            IMEEnabler = false;
        }

        public void ExecuteInterrupt(int b) {
            if (HALTED) {
                PC++;
                HALTED = false;
            }
            if (IME) {
                PUSH(PC);
                PC = (ushort)(0x40 + (8 * b));
                IME = false;
                mmu.setIF(bitClear(b, mmu.IF()));
            }
        }

        private void PUSH(ushort w) {// (SP - 1) < -PC.hi; (SP - 2) < -PC.lo
            SP = (ushort)(0xffff & (SP - 2));
            mmu.writeWord(SP, w);
        }

        private ushort POP() {
            ushort ret = mmu.readWord(SP);
            SP = (ushort)(0xffff & (SP + 2));
            //byte l = mmu.readByte(++SP);
            //byte h = mmu.readByte(++SP);
            //ushort ret = (ushort)(h << 8 | l);
            //Console.WriteLine("stack POP = " + ret.ToString("x4") + " SP = " + SP.ToString("x4") + " reading: " + mmu.readWord(SP).ToString("x4") + "ret = " /*+ ((ushort)(h << 8 | l)).ToString("x4")*/);


        return ret;
        }

        private void SetFlagZ(int b) {
            setFlagZ((byte)(0xff & b) == 0);
        }

        private void SetFlagC(int i) {
            setFlagC((i >> 8) != 0);
        }

        private void SetFlagH(byte b1, byte b2) {
            setFlagH(((b1 & 0xF) + (b2 & 0xF)) > 0xF);
        }

        private void SetFlagH(ushort w1, ushort w2) {
            setFlagH(((w1 & 0xFFF) + (w2 & 0xFFF)) > 0xFFF);
        }

        private void SetFlagHCarry(byte b1, byte b2) {
            setFlagH(((b1 & 0xF) + (b2 & 0xF)) >= 0xF);
        }

        private void SetFlagHSub(byte b1, byte b2) {
            setFlagH((b1 & 0xF) < (b2 & 0xF));
        }

        private void SetFlagHSubCarry(byte b1, byte b2) {
            int carry = getFlagC() ? 1 : 0;
            setFlagH((b1 & 0xF) < ((b2 & 0xF) + carry));
        }

        private void warnUnsupportedOpcode(byte opcode) {
            Debug.Log((PC - 1).ToString("x4") + " Unsupported operation " + opcode.ToString("x2"));
        }

        private int dev;
        private void debug(byte opcode) {
            dev += cycles;
            if (dev >= 23440108 /*&& PC == 0x35A*/) //0x100 23440108
                Debug.Log("Cycle " + dev + " PC " + (PC - 1).ToString("x4") + " Stack: " + SP.ToString("x4") + " AF: " + A.ToString("x2") + "" + F.ToString("x2")
                    + " BC: " + B.ToString("x2") + "" + C.ToString("x2") + " DE: " + D.ToString("x2") + "" + E.ToString("x2") + " HL: " + H.ToString("x2") + "" + L.ToString("x2")
                    + " op " + opcode.ToString("x2") + " D16 " + mmu.readWord(PC).ToString("x4") + " LY: " + mmu.LY().ToString("x2"));
        }


    }

