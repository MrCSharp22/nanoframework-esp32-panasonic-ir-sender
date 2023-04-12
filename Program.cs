using System;
using System.Threading;

using nanoFramework.Hardware.Esp32.Rmt;

namespace Panasonic_IR_Sender
{
    public static class Program
    {
        private const int TxChannelPinNumber = 16;

        private const ushort HeaderPulse = 3468;
        private const ushort HeaderSpace = 1767;
        private const ushort Pulse = 432;
        private const ushort ZeroSpace = 432;
        private const ushort OneSpace = 1296;
        private const ushort PauseSpace = 10000;

        private static RmtCommand Header = new RmtCommand(HeaderPulse, true, HeaderSpace, false);
        private static RmtCommand ZeroBit = new RmtCommand(Pulse, true, ZeroSpace, false);
        private static RmtCommand OneBit = new RmtCommand(Pulse, true, OneSpace, false);
        private static RmtCommand Pause = new RmtCommand(Pulse, true, PauseSpace, false);
        private static RmtCommand End = new RmtCommand(Pulse, true, 0, false);

        //public static byte[] StartingData = new byte[]
        //{
        //    //0    1     2     3     4     5     6     7
        //    0x40, 0x04, 0x07, 0x20, 0x00, 0x00, 0x00, 0x60, // Frame 1: Static value (8 bytes)

        //    // Frame 2:
        //    //8    9     10    11    12    13    14    15
        //    0x02, 0x20, 0xE0, 0x04, 0x00, 0x38, 0x20, 0x80,
        //    //16   17    18    19    20    21    22    23
        //    0x31, 0x00, 0x00, 0x0E, 0xE0, 0x00, 0x00, 0x81,
        //    //24   25
        //    0x00, 0x00, // Time
        //    //26
        //    0x7F // CRC
        //};

        public static byte[] StartingData = new byte[]
        {
            0x40, 0x04, 0x07, 0x20, 0x00, 0x00, 0x00, 0x60,
            0x02, 0x20, 0xE0, 0x04, 0x00, 0x38, 0x20, 0x80,
            0x31, 0x00, 0x00, 0x0E, 0xE0, 0x00, 0x00, 0x81,
            0x00, 0x00,
            0x7E
        };

        public static void Main()
        {
            var txChannelSettings = new TransmitChannelSettings(TxChannelPinNumber)
            {
                ClockDivider = 80,

                EnableCarrierWave = true,
                CarrierLevel = true,
                CarrierWaveFrequency = 38_000,
                CarrierWaveDutyPercentage = 50,

                IdleLevel = false,
                EnableIdleLevelOutput = true,

                NumberOfMemoryBlocks = 4,
                SignalInverterEnabled = false,
            };
            using var txChannel = new TransmitterChannel(txChannelSettings);
            txChannel.ClearCommands();

            Console.WriteLine($"Sending commands on RMT TX Channel: {txChannel.Channel}");

            txChannel.ClearCommands();

            txChannel.AddCommand(Header);
            txChannel.AddCommands(StartingData, 0, 8); // frame 1
            txChannel.AddCommand(Pause);
            txChannel.AddCommand(Header);
            txChannel.AddCommands(StartingData, 8, StartingData.Length - 1); // frame 2 without CRC

            var crc = CalcCrc(StartingData, 8, StartingData.Length - 1);
            txChannel.AddCommands(new byte[] { crc }, 0, 1); // CRC

            txChannel.AddCommand(End);

            while (true)
            {
                txChannel.Send(waitTxDone: true);

                Console.WriteLine("Sent! You should've heard a beep from the AC unit.");

                Thread.Sleep(2000);
            }
        }

        private static void AddCommands(this TransmitterChannel txChannel, byte[] data, int start, int end)
        {
            for (var i = start; i < end; i++)
            {
                byte b = data[i];
                for (byte mask = 0x01; mask > 0x00 && mask < 0xFF; mask <<= 1)
                {
                    if ((b & mask) > 0)
                    {
                        txChannel.AddCommand(OneBit);
                    }
                    else
                    {
                        txChannel.AddCommand(ZeroBit);
                    }
                }
            }
        }

        private static byte CalcCrc(byte[] data, int start, int end)
        {
            byte crc = 0x00;
            for (var i = start; i < end; i++)
            {
                crc += data[i];
            }

            return crc;
        }
    }
}
