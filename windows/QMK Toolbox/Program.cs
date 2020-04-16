﻿using System;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace QMK_Toolbox
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        ///

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        private const int AttachParentProcess = -1;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        private static readonly Mutex Mutex = new Mutex(true, "{8F7F0AC4-B9A1-45fd-A8CF-72F04E6BDE8F}");

        [STAThread]
        private static void Main(string[] args)
        {
            if (Mutex.WaitOne(TimeSpan.Zero, true) && args.Length > 0)
            {
                AttachConsole(AttachParentProcess);

                var printer = new Printing();
                if (args[0].Equals("list"))
                {
                    var flasher = new Flashing(printer);
                    var usb = new Usb(flasher, printer);
                    flasher.Usb = usb;

                    ManagementObjectCollection collection;
                    using (var searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE ""USB%"""))
                        collection = searcher.Get();

                    usb.DetectBootloaderFromCollection(collection);
                    FreeConsole();
                    Environment.Exit(0);
                }

                if (args[0].Equals("flash"))
                {
                    var flasher = new Flashing(printer);
                    var usb = new Usb(flasher, printer);
                    flasher.Usb = usb;

                    do
                    {
                        Thread.Sleep(500);

                        printer.Print("Waiting for device", MessageType.Info);
                        ManagementObjectCollection collection;
                        using (var searcher =
                            new ManagementObjectSearcher(@"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE ""USB%"""))
                            collection = searcher.Get();

                        usb.DetectBootloaderFromCollection(collection);
                    } while (!usb.AreDevicesAvailable());

                    if (usb.AreDevicesAvailable())
                    {
                        var mcu = args[1];
                        var filepath = args[2];
                        printer.Print("Attempting to flash, please don't remove device", MessageType.Bootloader);
                        flasher.Flash(mcu, filepath);
                        FreeConsole();
                        Environment.Exit(0);
                    }
                    else
                    {
                        printer.Print("There are no devices available", MessageType.Error);
                        FreeConsole();
                        Environment.Exit(1);
                    }
                }

                if (args[0].Equals("help"))
                {
                    printer.Print("QMK Toolbox (http://qmk.fm/toolbox)", MessageType.Info);
                    printer.PrintResponse("Supported bootloaders:\n", MessageType.Info);
                    printer.PrintResponse(" - Atmel/LUFA/QMK DFU via dfu-programmer (http://dfu-programmer.github.io/)\n", MessageType.Info);
                    printer.PrintResponse(" - Caterina (Arduino, Pro Micro) via avrdude (http://nongnu.org/avrdude/)\n", MessageType.Info);
                    printer.PrintResponse(" - Halfkay (Teensy, Ergodox EZ) via Teensy Loader (https://pjrc.com/teensy/loader_cli.html)\n", MessageType.Info);
                    printer.PrintResponse(" - ARM DFU (STM32, Kiibohd) via dfu-util (http://dfu-util.sourceforge.net/)\n", MessageType.Info);
                    printer.PrintResponse(" - Atmel SAM-BA (Massdrop) via Massdrop Loader (https://github.com/massdrop/mdloader)\n", MessageType.Info);
                    printer.PrintResponse(" - BootloadHID (Atmel, PS2AVRGB) via bootloadHID (https://www.obdev.at/products/vusb/bootloadhid.html)\n", MessageType.Info);
                    printer.PrintResponse("Supported ISP flashers:\n", MessageType.Info);
                    printer.PrintResponse(" - USBTiny (AVR Pocket)\n", MessageType.Info);
                    printer.PrintResponse(" - AVRISP (Arduino ISP)\n", MessageType.Info);
                    printer.PrintResponse(" - USBasp (AVR ISP)\n", MessageType.Info);
                    printer.PrintResponse("usage: qmk_toolbox.exe flash <mcu> <filepath>", MessageType.Info);
                    FreeConsole();
                    Environment.Exit(0);
                }

                printer.Print("Command not found - use \"help\" for all commands", MessageType.Error);
                FreeConsole();
                Environment.Exit(1);
            }
            else
            {
                if (Mutex.WaitOne(TimeSpan.Zero, true))
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(args.Length == 0 ? new MainWindow(string.Empty) : new MainWindow(args[0]));
                    Mutex.ReleaseMutex();
                }
                else
                {
                    AttachConsole(AttachParentProcess);
                    var printer = new Printing();
                    printer.Print("Instance of QMK Toolbox is already running.", MessageType.Error);
                }
            }
        }
    }

    // this class just wraps some Win32 stuff that we're going to use
    internal class NativeMethods
    {
        public const int HwndBroadcast = 0xffff;
        public static readonly int WmShowme = RegisterWindowMessage("WM_SHOWME");

        [DllImport("user32")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        [DllImport("user32")]
        public static extern int RegisterWindowMessage(string message);
    }
}
