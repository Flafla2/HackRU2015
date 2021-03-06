﻿using UnityEngine;
using System.Collections.Generic;
using System;
using System.Threading;
using System.Runtime.InteropServices;

namespace WiimoteApi { 

public delegate void ReadResponder(byte[] data);

public class WiimoteManager
{
    public const ushort vendor_id = 0x057e;
    public const ushort product_id_wiimote = 0x0306;
    public const ushort product_id_wiimoteplus = 0x0330;
    
    public static List<Wiimote> Wiimotes = new List<Wiimote>();

    private static InputDataType last_report_type = InputDataType.REPORT_BUTTONS;
    private static bool expecting_status_report = false;

    public static bool Debug_Messages = false;

    public static int MaxWriteFrequency = 20; // In ms
    private static float LastWriteTime = 0;
    private static Queue<WriteQueueData> WriteQueue;

    // ------------- RAW HIDAPI INTERFACE ------------- //

    public static bool FindWiimote(bool wiimoteplus)
    {
        //if (hidapi_wiimote != IntPtr.Zero)
        //    HIDapi.hid_close(hidapi_wiimote);

        IntPtr ptr = HIDapi.hid_enumerate(vendor_id, wiimoteplus ? product_id_wiimoteplus : product_id_wiimote);
        IntPtr cur_ptr = ptr;
        if (ptr == IntPtr.Zero)
            return false;

        hid_device_info enumerate = (hid_device_info)Marshal.PtrToStructure(ptr, typeof(hid_device_info));

        bool found = false;

        while(cur_ptr != IntPtr.Zero)
        {
            Wiimote remote = null;
            bool fin = false;
            foreach (Wiimote r in Wiimotes)
            {
                if (fin)
                    continue;

                if (r.hidapi_path.Equals(enumerate.path))
                {
                    remote = r;
                    fin = true;
                }
            }
            if (remote == null)
            {
                remote = new Wiimote();
                remote.hidapi_path = enumerate.path;
                
                if(Debug_Messages)
                    Debug.Log("Found New Remote: "+remote.hidapi_path);

                remote.hidapi_handle = HIDapi.hid_open_path(remote.hidapi_path);
                remote.wiimoteplus = wiimoteplus;
                Wiimotes.Add(remote);

                SendDataReportMode(remote, InputDataType.REPORT_BUTTONS);
                SendStatusInfoRequest(remote);
            }

            cur_ptr = enumerate.next;
            if(cur_ptr != IntPtr.Zero)
                enumerate = (hid_device_info)Marshal.PtrToStructure(cur_ptr, typeof(hid_device_info));
        }

        HIDapi.hid_free_enumeration(ptr);

        return found;
    }

    public static void Cleanup(Wiimote remote)
    {
        if (remote.hidapi_handle != IntPtr.Zero)
            HIDapi.hid_close(remote.hidapi_handle);

        Wiimotes.Remove(remote);
    }

    public static bool HasWiimote()
    {
        return !(Wiimotes.Count <= 0 || Wiimotes[0] == null || Wiimotes[0].hidapi_handle == IntPtr.Zero);
    }

    public static int SendRaw(IntPtr hidapi_wiimote, byte[] data)
    {
        if (hidapi_wiimote == IntPtr.Zero) return -2;

        if (WriteQueue == null)
        {
            WriteQueue = new Queue<WriteQueueData>();
            SendThreadObj = new Thread(new ThreadStart(SendThread));
            SendThreadObj.Start();
        }

        WriteQueueData wqd = new WriteQueueData();
        wqd.pointer = hidapi_wiimote;
        wqd.data = data;
        lock(WriteQueue)
            WriteQueue.Enqueue(wqd);

        return 0; // TODO: Better error handling
    }

    private static Thread SendThreadObj;
    private static void SendThread()
    {
        while (true)
        {
            lock (WriteQueue)
            {
                if (WriteQueue.Count != 0)
                {
                    WriteQueueData wqd = WriteQueue.Dequeue();
                    int res = HIDapi.hid_write(wqd.pointer, wqd.data, new UIntPtr(Convert.ToUInt32(wqd.data.Length)));
                    if (res == -1) Debug.LogError("HidAPI reports error " + res + " on write: " + Marshal.PtrToStringUni(HIDapi.hid_error(wqd.pointer)));
                    else if (Debug_Messages) Debug.Log("Sent " + res + "b: [" + wqd.data[0].ToString("X").PadLeft(2, '0') + "] " + BitConverter.ToString(wqd.data, 1));
                }
            }
            Thread.Sleep(MaxWriteFrequency);
        }
    }

    public static int RecieveRaw(IntPtr hidapi_wiimote, byte[] buf)
    {
        if (hidapi_wiimote == IntPtr.Zero) return -2;

        HIDapi.hid_set_nonblocking(hidapi_wiimote, 1);
        int res = HIDapi.hid_read(hidapi_wiimote, buf, new UIntPtr(Convert.ToUInt32(buf.Length)));

        return res;
    }

    // ------------- WIIMOTE SPECIFIC UTILITIES ------------- //

    #region Setups

    public static bool SetupIRCamera(Wiimote remote, IRDataType type = IRDataType.EXTENDED)
    {
        int res;
        // 1. Enable IR Camera (Send 0x04 to Output Report 0x13)
        // 2. Enable IR Camera 2 (Send 0x04 to Output Report 0x1a)
        res = SendIRCameraEnable(remote, true);
        if (res < 0) return false;
        // 3. Write 0x08 to register 0xb00030
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xb00030, new byte[] { 0x08 });
        if (res < 0) return false;
        // 4. Write Sensitivity Block 1 to registers at 0xb00000
        // Wii sensitivity level 3:
        // 02 00 00 71 01 00 aa 00 64
        // High Sensitivity:
        // 00 00 00 00 00 00 90 00 41
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xb00000, 
            new byte[] { 0x02, 0x00, 0x00, 0x71, 0x01, 0x00, 0xaa, 0x00, 0x64 });
            //new byte[] {0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x90, 0x00, 0x41});
        if (res < 0) return false;
        // 5. Write Sensitivity Block 2 to registers at 0xb0001a
        // Wii sensitivity level 3: 
        // 63 03
        // High Sensitivity:
        // 40 00
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xb0001a, new byte[] { 0x63, 0x03 });
        if (res < 0) return false;
        // 6. Write Mode Number to register 0xb00033
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xb00033, new byte[] { (byte)type });
        if (res < 0) return false;
        // 7. Write 0x08 to register 0xb00030 (again)
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xb00030, new byte[] { 0x08 });
        if (res < 0) return false;

        switch (type)
        {
            case IRDataType.BASIC:
                res = SendDataReportMode(remote, InputDataType.REPORT_BUTTONS_ACCEL_IR10_EXT6);
                break;
            case IRDataType.EXTENDED:
                res = SendDataReportMode(remote, InputDataType.REPORT_BUTTONS_ACCEL_IR12);
                break;
            case IRDataType.FULL:
                Debug.LogWarning("Interleaved data reporting is currently not supported.");
                res = SendDataReportMode(remote, InputDataType.REPORT_INTERLEAVED);
                break;
        }
        
        if (res < 0) return false;
        return true;
    }

    public static bool RequestIdentifyWiiMotionPlus(Wiimote remote)
    {
        int res;
        res = SendRegisterReadRequest(remote, RegisterType.CONTROL, 0xA600FA, 6, remote.RespondIdentifyWiiMotionPlus);
        return res > 0;
    }

    public static bool RequestIdentifyExtension(Wiimote remote)
    {
        int res = SendRegisterReadRequest(remote, RegisterType.CONTROL, 0xA400FA, 6, remote.RespondIdentifyExtension);
        return res > 0;
    }

    public static bool ActivateWiiMotionPlus(Wiimote remote)
    {
        if (!remote.wmp_attached)
            Debug.LogWarning("There is a request to activate the Wii Motion Plus even though it has not been confirmed to exist!  Trying anyway.");

        // Initialize the Wii Motion Plus by writing 0x55 to register 0xA600F0
        int res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xA600F0, new byte[] { 0x55 });
        if (res < 0) return false;

        // Activate the Wii Motion Plus as the active extension by writing 0x04 to register 0xA600FE
        // This does 3 things:
        // 1. A status report (0x20) will be sent, which indicates that an extension has been
        //    plugged in - IF there is no extension plugged into the passthrough port.
        // 2. The standard extension identifier at 0xA400FA now reads 00 00 A4 20 04 05
        // 3. Extension reports now contain Wii Motion Plus data.
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xA600FE, new byte[] { 0x04 });
        if (res < 0) return false;

        return true;
    }

    public static bool ActivateExtension(Wiimote remote)
    {
        if (!remote.ext_connected)
            Debug.LogWarning("There is a request to activate an Extension controller even though it has not been confirmed to exist!  Trying anyway.");

        // 1. Initialize the Extension by writing 0x55 to register 0xA400F0
        int res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xA400F0, new byte[] { 0x55 });
        if (res < 0) return false;

        // 2. Activate the Extension by writing 0x00 to register 0xA400FB
        res = SendRegisterWriteRequest(remote, RegisterType.CONTROL, 0xA400FB, new byte[] { 0x00 });
        if (res < 0) return false;
        return true;
    }

    #endregion

    #region Write
    public static int SendWithType(Wiimote remote, OutputDataType type, byte[] data)
    {
        byte[] final = new byte[data.Length + 1];
        final[0] = (byte)type;

        for (int x = 0; x < data.Length; x++)
            final[x + 1] = data[x];

        if (remote.RumbleOn)
            final[1] |= 0x01;

        int res = SendRaw(remote.hidapi_handle, final);

        if (res < -1) Debug.LogError("Incorrect Input to HIDAPI.  No data has been sent.");
        

        return res;
    }

    public static int SendPlayerLED(Wiimote remote, bool led1, bool led2, bool led3, bool led4)
    {
        byte mask = 0;
        if (led1) mask |= 0x10;
        if (led2) mask |= 0x20;
        if (led3) mask |= 0x40;
        if (led4) mask |= 0x80;

        return SendWithType(remote, OutputDataType.LED, new byte[] { mask });
    }

    public static int SendDataReportMode(Wiimote remote, InputDataType mode)
    {
        if (mode == InputDataType.STATUS_INFO || mode == InputDataType.READ_MEMORY_REGISTERS || mode == InputDataType.ACKNOWLEDGE_OUTPUT_REPORT)
        {
            Debug.LogError("Passed " + mode.ToString() + " to SendDataReportMode!");
            return -2;
        }

        last_report_type = mode;

        return SendWithType(remote, OutputDataType.DATA_REPORT_MODE, new byte[] { 0x00, (byte)mode });
    }

    public static int SendIRCameraEnable(Wiimote remote, bool enabled)
    {
        byte[] mask = new byte[] { (byte)(enabled ? 0x04 : 0x00) };

        int first = SendWithType(remote, OutputDataType.IR_CAMERA_ENABLE, mask);
        if (first < 0) return first;

        int second = SendWithType(remote, OutputDataType.IR_CAMERA_ENABLE_2, mask);
        if (second < 0) return second;

        return first + second; // success
    }

    public static int SendSpeakerEnabled(Wiimote remote, bool enabled)
    {
        byte[] mask = new byte[] { (byte)(enabled ? 0x04 : 0x00) };

        return SendWithType(remote, OutputDataType.SPEAKER_ENABLE, mask);
    }

    public static int SendSpeakerMuted(Wiimote remote, bool muted)
    {
        byte[] mask = new byte[] { (byte)(muted ? 0x04 : 0x00) };

        return SendWithType(remote, OutputDataType.SPEAKER_MUTE, mask);
    }

    public static int SendStatusInfoRequest(Wiimote remote)
    {
        expecting_status_report = true;
        return SendWithType(remote, OutputDataType.STATUS_INFO_REQUEST, new byte[] { 0x00 });
    }

    public static int SendRegisterReadRequest(Wiimote remote, RegisterType type, int offset, int size, ReadResponder Responder)
    {
        if (remote.CurrentReadData != null)
        {
            Debug.LogWarning("Aborting read request; There is already a read request pending!");
            return -2;
        }
            

        remote.CurrentReadData = new RegisterReadData(offset, size, Responder);

        byte address_select = (byte)type;
        byte[] offsetArr = IntToBigEndian(offset, 3);
        byte[] sizeArr = IntToBigEndian(size, 2);

        byte[] total = new byte[] { address_select, offsetArr[0], offsetArr[1], offsetArr[2], 
            sizeArr[0], sizeArr[1] };

        return SendWithType(remote, OutputDataType.READ_MEMORY_REGISTERS, total);
    }

    public static int SendRegisterWriteRequest(Wiimote remote, RegisterType type, int offset, byte[] data)
    {
        if (data.Length > 16) return -2;
        

        byte address_select = (byte)type;
        byte[] offsetArr = IntToBigEndian(offset,3);

        byte[] total = new byte[21];
        total[0] = address_select;
        for (int x = 0; x < 3; x++) total[x + 1] = offsetArr[x];
        total[4] = (byte)data.Length;
        for (int x = 0; x < data.Length; x++) total[x + 5] = data[x];

        return SendWithType(remote, OutputDataType.WRITE_MEMORY_REGISTERS, total);
    }
    #endregion

    #region Read
    public static int ReadWiimoteData(Wiimote remote)
    {
        byte[] buf = new byte[22];
        int status = RecieveRaw(remote.hidapi_handle, buf);
        if (status <= 0) return status; // Either there is some sort of error or we haven't recieved anything

        int typesize = GetInputDataTypeSize((InputDataType)buf[0]);
        byte[] data = new byte[typesize];
        for (int x = 0; x < data.Length; x++)
            data[x] = buf[x + 1];

        if (Debug_Messages)
            Debug.Log("Recieved: [" + buf[0].ToString("X").PadLeft(2, '0') + "] " + BitConverter.ToString(data));

        // Variable names used throughout the switch/case block
        byte[] buttons;
        byte[] accel;
        byte[] ext;
        byte[] ir;

        switch ((InputDataType)buf[0]) // buf[0] is the output ID byte
        {
            case InputDataType.STATUS_INFO: // done.
                buttons = new byte[] { data[0], data[1] };
                byte flags = data[2];
                byte battery_level = data[5];

                InterpretButtonData(remote, buttons);
                remote.battery_level = battery_level;

                bool old_ext_connected = remote.ext_connected;

                remote.battery_low = (flags & 0x01) == 0x01;
                remote.ext_connected = (flags & 0x02) == 0x02;
                remote.speaker_enabled = (flags & 0x04) == 0x04;
                remote.ir_enabled = (flags & 0x08) == 0x08;
                remote.led[0] = (flags & 0x10) == 0x10;
                remote.led[1] = (flags & 0x20) == 0x20;
                remote.led[2] = (flags & 0x40) == 0x40;
                remote.led[3] = (flags & 0x80) == 0x80;

                if (expecting_status_report)
                {
                    expecting_status_report = false;
                }
                else                                        // We haven't requested any data report type, meaning a controller has connected.
                {
                    Debug.Log("An extension has been connected or disconnected.");
                    SendDataReportMode(remote, last_report_type);   // If we don't update the data report mode, no updates will be sent
                }

                if (remote.ext_connected != old_ext_connected)
                {
                    if (remote.ext_connected)                    // The wiimote doesn't allow reading from the extension identifier
                    {                                               // when nothing is connected.
                        ActivateExtension(remote);
                        RequestIdentifyExtension(remote);         // Identify what extension was connected.
                    } else
                    {
                        remote.current_ext = ExtensionController.NONE;
                    }
                }
                break;
            case InputDataType.READ_MEMORY_REGISTERS: // done.
                buttons = new byte[] { data[0], data[1] };
                InterpretButtonData(remote, buttons);

                if (remote.CurrentReadData == null)
                {
                    Debug.LogWarning("Recived Register Read Report when none was expected.  Ignoring.");
                    return status;
                }

                byte size = (byte)((data[2] >> 4) + 0x01);
                byte error = (byte)(data[2] & 0x0f);
                if (error == 0x07)
                {
                    Debug.LogError("Wiimote reports Read Register error 7: Attempting to read from a write-only register.  Aborting read.");
                    remote.CurrentReadData = null;
                    return status;
                }
                // lowOffset is reversed because the wiimote reports are in Big Endian order
                ushort lowOffset = BitConverter.ToUInt16(new byte[] { data[4], data[3] }, 0);
                ushort expected = (ushort)remote.CurrentReadData.ExpectedOffset;
                if (expected != lowOffset)
                    Debug.LogWarning("Expected Register Read Offset (" + expected + ") does not match reported offset from Wiimote (" + lowOffset + ")");
                byte[] read = new byte[size];
                for (int x = 0; x < size; x++)
                    read[x] = data[x + 5];

                remote.CurrentReadData.AppendData(read);
                if (remote.CurrentReadData.ExpectedOffset >= remote.CurrentReadData.Offset + remote.CurrentReadData.Size)
                    remote.CurrentReadData = null;

                break;
            case InputDataType.ACKNOWLEDGE_OUTPUT_REPORT:
                buttons = new byte[] { data[0], data[1] };
                InterpretButtonData(remote, buttons);
                // TODO: doesn't do any actual error handling, or do any special code about acknowledging the output report.
                break;
            case InputDataType.REPORT_BUTTONS: // done.
                buttons = new byte[] { data[0], data[1] };
                InterpretButtonData(remote, buttons);
                break;
            case InputDataType.REPORT_BUTTONS_ACCEL: // done.
                buttons = new byte[] { data[0], data[1] };
                InterpretButtonData(remote, buttons);

                accel = new byte[] { data[2], data[3], data[4] };
                InterpretAccelData(remote, buttons, accel);
                break;
            case InputDataType.REPORT_BUTTONS_EXT8: // done.
                buttons = new byte[] { data[0], data[1] };
                InterpretButtonData(remote, buttons);

                ext = new byte[8];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x + 2];

                remote.extension = ext;
                break;
            case InputDataType.REPORT_BUTTONS_ACCEL_IR12: // done.
                buttons = new byte[] { data[0], data[1] };
                InterpretButtonData(remote, buttons);

                accel = new byte[] { data[2], data[3], data[4] };
                InterpretAccelData(remote, buttons, accel);

                ir = new byte[12];
                for (int x = 0; x < 12; x++)
                    ir[x] = data[x + 5];
                InterpretIRData12(remote, ir);
                break;
            case InputDataType.REPORT_BUTTONS_EXT19: // done.
                buttons = new byte[] { data[0], data[1] };
                InterpretButtonData(remote, buttons);

                ext = new byte[19];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x + 2];
                remote.extension = ext;
                break;
            case InputDataType.REPORT_BUTTONS_ACCEL_EXT16: // done.
                buttons = new byte[] { data[0], data[1] };
                InterpretButtonData(remote, buttons);

                accel = new byte[] { data[2], data[3], data[4] };
                InterpretAccelData(remote, buttons, accel);

                ext = new byte[16];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x + 5];
                remote.extension = ext;
                break;
            case InputDataType.REPORT_BUTTONS_IR10_EXT9: // done.
                buttons = new byte[] { data[0], data[1] };
                InterpretButtonData(remote, buttons);

                ir = new byte[10];
                for (int x = 0; x < 10; x++)
                    ir[x] = data[x + 2];
                InterpretIRData10(remote, ir);

                ext = new byte[9];
                for (int x = 0; x < 9; x++)
                    ext[x] = data[x + 12];
                remote.extension = ext;
                break;
            case InputDataType.REPORT_BUTTONS_ACCEL_IR10_EXT6: // done.
                buttons = new byte[] { data[0], data[1] };
                InterpretButtonData(remote, buttons);

                accel = new byte[] { data[2], data[3], data[4] };
                InterpretAccelData(remote, buttons, accel);

                ir = new byte[10];
                for (int x = 0; x < 10; x++)
                    ir[x] = data[x + 5];
                InterpretIRData10(remote, ir);

                ext = new byte[6];
                for (int x = 0; x < 6; x++)
                    ext[x] = data[x + 15];
                remote.extension = ext;
                break;
            case InputDataType.REPORT_EXT21: // done.
                ext = new byte[21];
                for (int x = 0; x < ext.Length; x++)
                    ext[x] = data[x];
                remote.extension = ext;
                break;
            case InputDataType.REPORT_INTERLEAVED:
                // TODO
                break;
            case InputDataType.REPORT_INTERLEAVED_ALT:
                // TODO
                break;
        }
        return status;
    }

    public static int GetInputDataTypeSize(InputDataType type)
    {
        switch (type)
        {
            case InputDataType.STATUS_INFO:
                return 6;
            case InputDataType.READ_MEMORY_REGISTERS:
                return 21;
            case InputDataType.ACKNOWLEDGE_OUTPUT_REPORT:
                return 4;
            case InputDataType.REPORT_BUTTONS:
                return 2;
            case InputDataType.REPORT_BUTTONS_ACCEL:
                return 5;
            case InputDataType.REPORT_BUTTONS_EXT8:
                return 10;
            case InputDataType.REPORT_BUTTONS_ACCEL_IR12:
                return 17;
            case InputDataType.REPORT_BUTTONS_EXT19:
                return 21;
            case InputDataType.REPORT_BUTTONS_ACCEL_EXT16:
                return 21;
            case InputDataType.REPORT_BUTTONS_IR10_EXT9:
                return 21;
            case InputDataType.REPORT_BUTTONS_ACCEL_IR10_EXT6:
                return 21;
            case InputDataType.REPORT_EXT21:
                return 21;
            case InputDataType.REPORT_INTERLEAVED:
                return 21;
            case InputDataType.REPORT_INTERLEAVED_ALT:
                return 21;
        }
        return 0;
    }

    public static void InterpretButtonData(Wiimote remote, byte[] data)
    {
        if (data == null || data.Length != 2) return;

        remote.d_left = (data[0] & 0x01) == 0x01;
        remote.d_right = (data[0] & 0x02) == 0x02;
        remote.d_down = (data[0] & 0x04) == 0x04;
        remote.d_up = (data[0] & 0x08) == 0x08;
        remote.plus = (data[0] & 0x10) == 0x10;

        remote.two = (data[1] & 0x01) == 0x01;
        remote.one = (data[1] & 0x02) == 0x02;
        remote.b = (data[1] & 0x04) == 0x04;
        remote.a = (data[1] & 0x08) == 0x08;
        remote.minus = (data[1] & 0x10) == 0x10;

        remote.home = (data[1] & 0x80) == 0x80;
    }

    public static void InterpretAccelData(Wiimote remote, byte[] buttons, byte[] accel)
    {
        if (buttons == null || accel == null || buttons.Length != 2 || accel.Length != 3) return;

        remote.accel[0] = ((int)accel[0] << 2) | ((buttons[0] >> 5) & 0xff);
        remote.accel[1] = ((int)accel[1] << 2) | ((buttons[1] >> 4) & 0xf0);
        remote.accel[2] = ((int)accel[2] << 2) | ((buttons[1] >> 5) & 0xf0);

        for (int x = 0; x < 3; x++) remote.accel[x] -= 0x200; // center around zero.
    }

    public static void InterpretIRData10(Wiimote remote, byte[] data)
    {
        if (data.Length != 10) return;

        byte[] half = new byte[5];
        for (int x = 0; x < 5; x++) half[x] = data[x];
        int[,] subset = InterperetIRData10_Subset(half);
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 3; y++)
                remote.ir[x, y] = subset[x, y];

        for (int x = 0; x < 5; x++) half[x] = data[x + 5];
        subset = InterperetIRData10_Subset(half);
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 3; y++)
                remote.ir[x+2, y] = subset[x, y];
    }

    private static int[,] InterperetIRData10_Subset(byte[] data)
    {
        if (data.Length != 5) return new int[,] { { -1, -1, -1 }, { -1, -1, -1 } };

        int x1 = data[0];
        x1 |= ((int)(data[2] & 0x30)) << 4;
        int y1 = data[1];
        y1 |= ((int)(data[2] & 0xc0)) << 2;

        if (data[0] == 0xff && data[1] == 0xff && (data[2] & 0xf0) == 0xf0)
        {
            x1 = -1;
            y1 = -1;
        }

        int x2 = data[3];
        x2 |= ((int)(data[2] & 0x03)) << 8;
        int y2 = data[4];
        y2 |= ((int)(data[2] & 0x0c)) << 6;

        if (data[3] == 0xff && data[4] == 0xff && (data[2] & 0x0f) == 0x0f)
        {
            x2 = -1;
            y2 = -1;
        }

        return new int[,] { { x1, y1, -1 }, { x2, y2, -1 } };
    }

    public static void InterpretIRData12(Wiimote remote, byte[] data)
    {
        if (data.Length != 12) return;
        for (int x = 0; x < 4; x++)
        {
            int i = x * 3; // starting index of data
            byte[] subset = new byte[] { data[i], data[i + 1], data[i + 2] };
            int[] calc = InterpretIRData12_Subset(subset);

            remote.ir[x, 0] = calc[0];
            remote.ir[x, 1] = calc[1];
            remote.ir[x, 2] = calc[2];
        }
    }

    private static int[] InterpretIRData12_Subset(byte[] data)
    {
        if (data.Length != 3) return new int[] { -1, -1, -1 };
        if (data[0] == 0xff && data[1] == 0xff && data[2] == 0xff) return new int[] { -1, -1, -1 };

        int x = data[0];
        x |= ((int)(data[2] & 0x30)) << 4;
        int y = data[1];
        y |= ((int)(data[2] & 0xc0)) << 2;
        int size = data[2] & 0x0f;

        return new int[] { x, y, size };
    }
    #endregion

    // ------------- UTILITY ------------- //
    public static byte[] IntToBigEndian(int input, int len)
    {
        byte[] intBytes = BitConverter.GetBytes(input);
        Array.Resize(ref intBytes, len);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(intBytes);
        
        return intBytes;
    }

    public enum RegisterType
    {
        EEPROM = 0x00, CONTROL = 0x04
    }

    /// <summary>
    /// A so-called output data type represents all data that can be sent from the host to the wiimote.
    /// This information is used by the remote to change its internal read/write remote.
    /// </summary>
    public enum OutputDataType
    {
        LED = 0x11,
        DATA_REPORT_MODE = 0x12,
        IR_CAMERA_ENABLE = 0x13,
        SPEAKER_ENABLE = 0x14,
        STATUS_INFO_REQUEST = 0x15,
        WRITE_MEMORY_REGISTERS = 0x16,
        READ_MEMORY_REGISTERS = 0x17,
        SPEAKER_DATA = 0x18,
        SPEAKER_MUTE = 0x19,
        IR_CAMERA_ENABLE_2 = 0x1A
    }

    /// <summary>
    /// A so-called input data type represents all data that can be sent from the wiimote to the host.
    /// This information is used by the host as basic controller data from the wiimote.
    /// 
    /// Note that all REPORT_ types represent the actual data types that can be sent from the contoller.
    /// </summary>
    public enum InputDataType
    {
        STATUS_INFO = 0x20,
        READ_MEMORY_REGISTERS = 0x21,
        ACKNOWLEDGE_OUTPUT_REPORT = 0x22,
        REPORT_BUTTONS = 0x30,
        REPORT_BUTTONS_ACCEL = 0x31,
        REPORT_BUTTONS_EXT8 = 0x32,
        REPORT_BUTTONS_ACCEL_IR12 = 0x33,
        REPORT_BUTTONS_EXT19 = 0x34,
        REPORT_BUTTONS_ACCEL_EXT16 = 0x35,
        REPORT_BUTTONS_IR10_EXT9 = 0x36,
        REPORT_BUTTONS_ACCEL_IR10_EXT6 = 0x37,
        REPORT_EXT21 = 0x3d,
        REPORT_INTERLEAVED = 0x3e,
        REPORT_INTERLEAVED_ALT = 0x3f
    }

    /// <summary>
    /// These are the 3 types of IR data accepted by the Wiimote.  They offer more
    /// or less IR data in exchange for space for other data (such as extension
    /// controllers or accelerometer data).  The 3 types are:
    /// 
    /// BASIC:      10 bytes of data.  Contains position data for each dot only.
    ///             Works with reports 0x36 and 0x37.
    /// EXTENDED:   12 bytes of data.  Contains position and size data for each dot.
    ///             Works with report 0x33 only.
    /// FULL:       36 bytes of data.  Contains position, size, bounding box, and
    ///             intensity data for each dot.  Works with interleaved report 
    ///             0x3e/0x3f only.
    /// </summary>
    public enum IRDataType
    {
        BASIC = 1,
        EXTENDED = 3,
        FULL = 5
    }

    private class WriteQueueData {
        public IntPtr pointer;
        public byte[] data;
    }

}

[System.Serializable]
public class Wiimote
{

    public Wiimote()
    {
        led = new bool[4];
        accel = new int[3];
        ir = new int[4,3];
    }

    public RegisterReadData CurrentReadData = null;
    public IntPtr hidapi_handle = IntPtr.Zero;
    public string hidapi_path;
    public bool wiimoteplus = false;
    public bool RumbleOn = false;

    public bool[] led;
    // Current wiimote-space accelration, in wiimote coordinate system.
    // These are RAW values, so they may be off.  See CalibrateAccel().
    // This is only updated if the Wiimote has a report mode with Accel
    // Range:            -128 to 128
    // Up/Down:          +Z/-Z
    // Left/Right:       +X/-Z
    // Forward/Backward: -Y/+Y
    public int[] accel;
    
    // Current wiimote RAW IR data.  Size = [4,3].  Wiimote IR data can
    // detect up to four IR dots.  Data = -1 if it is inapplicable (for
    // example, if there are less than four dots, or if size data is
    // unavailable).
    // This is only updated if the Wiimote has a report mode with IR
    //
    //        | Position X | Position Y |  Size  |
    // Range: |  0 - 1023  |  0 - 767   | 0 - 15 |
    // Index: |     0      |      1     |   2    |
    //
    // int[dot index, x (0) / y (1) / size (2)]
    public int[,] ir;

    // Raw data for any extension controllers connected to the wiimote
    // This is only updated if the Wiimote has a report mode with Extensions.
    public byte[] extension;

    // True if the Wiimote's batteries are low, as reported by the Wiimote.
    // This is only updated when the Wiimote sends status reports.
    // See also: battery_level
    public bool battery_low;
    // True if an extension controller is connected, as reported by the Wiimote.
    // This is only updated when the Wiimote sends status reports.
    public bool ext_connected;
    // True if the speaker is currently enabled, as reported by the Wiimote.
    // This is only updated when the Wiimote sends status reports.
    public bool speaker_enabled;
    // True if IR is currently enabled, as reported by the Wiimote.
    // This is only updated when the Wiimote sends status reports.
    public bool ir_enabled;

    // The current battery level, as reported by the Wiimote.
    // This is only updated when the Wiimote sends status reports.
    // See also: battery_low
    public byte battery_level;

    // Button: D-Pad Left
    public bool d_left;
    // Button: D-Pad Right
    public bool d_right;
    // Button: D-Pad Up
    public bool d_up;
    // Button: D-Pad Down
    public bool d_down;
    // Button: A
    public bool a;
    // Button: B
    public bool b;
    // Button: 1 (one)
    public bool one;
    // Button: 2 (two)
    public bool two;
    // Button: + (plus)
    public bool plus;
    // Button: - (minus)
    public bool minus;
    // Button: Home
    public bool home;

    // Calibration data for the accelerometer.  This is not reported
    // by the wiimote directly - it is instead collected from normal
    // Wiimote accelerometer data.  Here are the 3 calibration steps:
    //
    // 1. Horizontal with the A button facing up
    // 2. IR sensor down on the table so the expansion port is facing up
    // 3. Laying on its side, so the left side is facing up
    // 
    // By default it is set to experimental calibration data.
    // 
    // int[calibration step,calibration data] (size 3x3)
    public int[,] accel_calib = {
                                    { -20, -16,  83 },
                                    { -20,  80, -20 },
                                    {  84, -20, -12 }
                                };

    // True if a Wii Motion Plus is attached to the Wiimote, and it
    // has NOT BEEN ACTIVATED.  When the WMP is activated this value is
    // false.  This is only updated when the remote is requested from
    // Wiimote registers (see: RequestIdentifyWiiMotionPlus())
    public bool wmp_attached = false;
    public ExtensionController current_ext = ExtensionController.NONE;

    public void CalibrateAccel(AccelCalibrationStep step)
    {
        for (int x = 0; x < 3; x++)
            accel_calib[(int)step, x] = accel[x];
    }

    public float[] GetAccelZeroPoints()
    {
        float[] ret = new float[3];
        ret[0] = ((float)accel_calib[0, 0] / (float)accel_calib[1, 0]) / 2f;
        ret[1] = ((float)accel_calib[0, 1] / (float)accel_calib[2, 1]) / 2f;
        ret[2] = ((float)accel_calib[1, 2] / (float)accel_calib[2, 2]) / 2f;
        return ret;
    }

    // Returns the position at which the wiimote is pointing to.  This is a value from 0-1
    // representing the screen-space pointing position in X and Y.  Assume a 4x3 aspect ratio.
    public float[] GetPointingPosition()
    {
        float[] ret = new float[2];
        float[] midpoint = GetIRMidpoint();
        if (midpoint[0] < 0 || midpoint[1] < 0)
            return new float[] { -1, -1 };
        midpoint[0] = 1 - midpoint[0] - 0.5f;
        midpoint[1] = midpoint[1] - 0.5f;

        float rotation = Mathf.Atan2(accel[2], accel[0]) - (float)(Mathf.PI / 2.0f);
        float cos = Mathf.Cos(rotation);
        float sin = Mathf.Sin(rotation);
        ret[0] =  midpoint[0] * cos + midpoint[1] * sin;
        ret[1] = -midpoint[0] * sin + midpoint[1] * cos;
        ret[0] += 0.5f;
        ret[1] += 0.5f;

        ret[1] = 1 - ret[1];

        return ret;
    }

    // Returns the midpoint of all IR dots, or [0, 0] if none are found.  This is a value from 0-1
    // representing the screen-space position in X and Y.
    public float[] GetIRMidpoint()
    {
        float[] ret = new float[2];
        float[,] sensorIR = GetProbableSensorBarIR();
        ret[0] = sensorIR[0, 0] + sensorIR[1, 0];
        ret[1] = sensorIR[0, 1] + sensorIR[1, 1];
        ret[0] /= 2f * 1023f;
        ret[1] /= 2f * 767f;
        
        return ret;
    }

    private float[] LastIRSeparation = new float[] {0,0};
    public float[,] GetProbableSensorBarIR()
    {
        int count = 0;
        int[] ind = new int[2];
        for (int x = 0; x < 4; x++)
        {
            if (count > 1 || ir[x, 0] == -1 || ir[x, 1] == -1)
                continue;

            ind[count] = x;
            if (count == 1 && ir[ind[0], 0] > ir[x, 0])
            {
                ind[1] = ind[0];
                ind[0] = x;
            }
            count++;
        }

        if (count < 2)
            return new float[,] { { -1, -1, -1 }, { -1, -1, -1 } };

        float[,] ret = new float[2, 2];
        for (int x = 0; x < count; x++)
        {
            for(int y = 0; y < 2; y++)
                ret[x, y] = ir[ind[x], y];
        }

        if (count == 1) // one of the dots are outside of the wiimote FOV
        {
            
            if (ret[0,0] < 1023 / 2) // Left side of the screen, means that it's the right dot
            {
                for (int x = 0; x < 2; x++)
                {
                    ret[1, x] = ret[0, x];
                    ret[0, x] -= LastIRSeparation[x];
                }
            }
            else
            {
                for (int x = 0; x < 2; x++)
                {
                    ret[1, x] = ret[0, x];
                    ret[1, x] += LastIRSeparation[x];
                }
            }
        }
        else if (count == 2)
        {
            LastIRSeparation[0] = Mathf.Abs(ret[0, 0] - ret[1, 0]);
            LastIRSeparation[1] = Mathf.Abs(ret[0, 1] - ret[1, 1]);
        }

        return ret;
    }

    // Calibrated Accelerometer Data using experimental calibration points.
    // These values are in Wiimote coordinates (in the direction of gravity)
    // Range: -1 to 1
    // Up/Down:          +Z/-Z
    // Left/Right:       +X/-Z
    // Forward/Backward: -Y/+Y
    // See Also: CalibrateAccel(), GetAccelZeroPoints(), accel[]
    public float[] GetCalibratedAccelData()
    {
        float[] o = GetAccelZeroPoints();

        float x_raw = accel[0];
        float y_raw = accel[1];
        float z_raw = accel[2];

        float[] ret = new float[3];
        ret[0] = (x_raw - o[0]) / (accel_calib[2, 0] - o[0]);
        ret[1] = (y_raw - o[1]) / (accel_calib[1, 1] - o[1]);
        ret[2] = (z_raw - o[2]) / (accel_calib[0, 2] - o[2]);
        return ret;
    }

    public static byte[] ID_InactiveMotionPlus = new byte[] {0x00, 0x00, 0xA6, 0x20, 0x00, 0x05};

    public void RespondIdentifyWiiMotionPlus(byte[] data)
    {
        if (data.Length != ID_InactiveMotionPlus.Length)
        {
            wmp_attached = false;
            return;
        }
        for (int x = 0; x < data.Length; x++)
        {
            if (data[x] != ID_InactiveMotionPlus[x])
            {
                wmp_attached = false;
                return;
            }
        }
        wmp_attached = true;
    }

    public const long ID_ActiveMotionPlus           = 0x0000A4200405;
    public const long ID_ActiveMotionPlus_Nunchuck  = 0x0000A4200505;
    public const long ID_ActiveMotionPlus_Classic   = 0x0000A4200705;
    public const long ID_Nunchuck                   = 0x0000A4200000;
    public const long ID_Classic                    = 0x0000A4200101;
    public const long ID_ClassicPro                 = 0x0100A4200101;


    public void RespondIdentifyExtension(byte[] data)
    {
        if (data.Length != 6)
            return;

        byte[] resized = new byte[8];
        for (int x = 0; x < 6; x++) resized[x] = data[5-x];
        long val = BitConverter.ToInt64(resized, 0);

        if (val == ID_ActiveMotionPlus)
            current_ext = ExtensionController.MOTIONPLUS;
        else if (val == ID_ActiveMotionPlus_Nunchuck)
            current_ext = ExtensionController.MOTIONPLUS_NUNCHUCK;
        else if (val == ID_ActiveMotionPlus_Classic)
            current_ext = ExtensionController.MOTIONPLUS_CLASSIC;
        else if (val == ID_ClassicPro)
            current_ext = ExtensionController.CLASSIC_PRO;
        else if (val == ID_Nunchuck)
            current_ext = ExtensionController.NUNCHUCK;
        else if (val == ID_Classic)
            current_ext = ExtensionController.CLASSIC;
        else
            current_ext = ExtensionController.NONE;
    }
}

public class NunchuckData
{
    // Nunchuck Acceleration values.  These are in the same (RAW) format
    // as Wiimote.accel[].
    public int[] accel;
    // Nunchuck Analog Stick values.  This is a size 2 Array [X, Y] of
    // RAW (unprocessed) stick data.  Generally the analog stick returns
    // values in the range 35-228 for X and 27-220 for Y.  The center for
    // both is around 128.
    public byte[] stick;
    // If the C button has been pressed
    public bool c;
    // If the Z button has been pressed
    public bool z;

    public NunchuckData()
    {
        accel = new int[3];
        stick = new byte[2];
    }

    public void InterpretExtensionData(byte[] data) {
        if(data == null || data.Length < 6) {
            accel[0] = 0; accel[1] = 0; accel[2] = 0;
            stick[0] = 128; stick[1] = 128;
            c = false;
            z = false;
            return;
        }

        stick[0] = data[0];
        stick[1] = data[1];

        accel[0] = (int)data[2] << 2; accel[0] |= (data[5] & 0xc0) >> 6;
        accel[1] = (int)data[3] << 2; accel[1] |= (data[5] & 0x30) >> 4;
        accel[2] = (int)data[4] << 2; accel[2] |= (data[5] & 0x0c) >> 2;

        c = (data[5] & 0x02) == 0x02;
        z = (data[5] & 0x01) == 0x01;
    }

    public float[] GetStick01() {
        float[] ret = new float[2];
        ret[0] = stick[0];
        ret[0] -= 35;
        ret[1] = stick[1];
        ret[1] -= 27;
        for(int x=0;x<2;x++) {
            ret[x] /= 193f;
        }
        return ret;
    }
}

public class WiimotePlusData
{
    public int PitchSpeed = 0;
    public int YawSpeed = 0;
    public int RollSpeed = 0;
    public bool PitchSlow = false;
    public bool YawSlow = false;
    public bool RollSlow = false;
    public bool ExtensionConnected = false;

    public void InterpretExtensionData(byte[] data)
    {
        if (data.Length < 6)
        {
            PitchSpeed = 0;
            YawSpeed = 0;
            RollSpeed = 0;
            PitchSlow = false;
            YawSlow = false;
            RollSlow = false;
            ExtensionConnected = false;
            return;
        }

        YawSpeed = data[0];
        YawSpeed |= (int)(data[3] & 0xfc) << 6;
        RollSpeed = data[1];
        RollSpeed |= (int)(data[4] & 0xfc) << 6;
        PitchSpeed = data[2];
        PitchSpeed |= (int)(data[5] & 0xfc) << 6;

        YawSlow = (data[3] & 0x02) == 0x02;
        PitchSlow = (data[3] & 0x01) == 0x01;
        RollSlow = (data[4] & 0x02) == 0x02;
        ExtensionConnected = (data[4] & 0x01) == 0x01;
    }
}

public enum ExtensionController
{
    NONE, NUNCHUCK, CLASSIC, CLASSIC_PRO, MOTIONPLUS, MOTIONPLUS_NUNCHUCK, MOTIONPLUS_CLASSIC
}

public enum AccelCalibrationStep {
    A_BUTTON_UP = 0,
    EXPANSION_UP = 1,
    LEFT_SIDE_UP = 2
}

public class RegisterReadData
{
    public RegisterReadData(int Offset, int Size, ReadResponder Responder)
    {
        _Offset = Offset;
        _Size = Size;
        _Buffer = new byte[Size];
        _ExpectedOffset = Offset;
        _Responder = Responder;
    }

    public int ExpectedOffset
    {
        get { return _ExpectedOffset; }
    }
    private int _ExpectedOffset;

    public byte[] Buffer
    {
        get { return _Buffer; }
    }
    private byte[] _Buffer;

    public int Offset
    {
        get { return _Offset; }
    }
    private int _Offset;

    public int Size
    {
        get { return _Size; }
    }
    private int _Size;

    private ReadResponder _Responder;

    public bool AppendData(byte[] data)
    {
        int start = _ExpectedOffset - _Offset;
        int end = start + data.Length;

        if (end > _Buffer.Length)
            return false;

        for (int x = start; x < end; x++)
        {
            _Buffer[x] = data[x - start];
        }

        _ExpectedOffset += data.Length;

        if (_ExpectedOffset >= _Offset + _Size)
            _Responder(_Buffer);

        return true;
    }

}
}