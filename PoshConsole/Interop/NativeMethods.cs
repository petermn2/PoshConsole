﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation.Host;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace PoshConsole.Interop
{
    public static partial class NativeMethods
    {
        [DllImport("user32.dll")]
        public static extern int ToAsciiEx(uint uVirtKey, uint uScanCode, byte[] lpKeyState, [Out] StringBuilder lpChar, uint uFlags, IntPtr hkl);

        /// <summary>
        /// The ToAscii function translates the specified virtual-key code and keyboard state 
        /// to the corresponding character or characters. The function translates the code using 
        /// the Input language and physical keyboard layout identified by the keyboard layout handle.
        /// </summary>
        /// <param name="uVirtKey">Specifies the virtual-key code to be translated.</param>
        /// <param name="uScanCode">Specifies the hardware scan code of the key to be translated. 
        /// The high-order bit of this value is set if the key is up (not pressed). </param>
        /// <param name="lpKeyState">Pointer to a 256-byte array that contains the current keyboard state. 
        /// Each element (byte) in the array contains the state of one key. If the high-order bit of a byte is set, 
        /// the key is down (pressed). The low bit, if set, indicates that the key is toggled on. In this function, 
        /// only the toggle bit of the CAPS LOCK key is relevant. The toggle state of the NUM LOCK and SCROLL LOCK keys is ignored.</param>
        /// <param name="lpChar">Pointer to the buffer that receives the translated character or characters.</param>
        /// <param name="uFlags">Specifies whether a menu is active. This parameter must be 1 if a menu is active, or 0 otherwise.</param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern int ToAscii(uint uVirtKey, uint uScanCode, byte[] lpKeyState, [Out] StringBuilder lpChar, uint uFlags);

        /// <summary>
        /// The ToUnicode function translates the specified virtual-key code and keyboard state 
        /// to the corresponding Unicode character or characters. To specify a handle to the keyboard layout 
        /// to use to translate the specified code, use the ToUnicodeEx function.
        /// </summary>
        /// <param name="wVirtKey">Specifies the virtual-key code to be translated.</param>
        /// <param name="wScanCode">Specifies the hardware scan code of the key to be translated. 
        /// The high-order bit of this value is set if the key is up.</param>
        /// <param name="lpKeyState">Pointer to a 256-byte array that contains the current keyboard state.
        ///     Each element (byte) in the array contains the state of one key. 
        ///     If the high-order bit of a byte is set, the key is down.</param>
        /// <param name="pwszBuff">Pointer to the buffer that receives the translated Unicode character 
        ///     or characters. However, this buffer may be returned without being null-terminated 
        ///     even though the variable name suggests that it is null-terminated.
        /// </param>
        /// <param name="cchBuff">Specifies the size, in wide characters, of the buffer pointed to by the pwszBuff parameter.</param>
        /// <param name="wFlags">Specifies the behavior of the function. If bit 0 is set, a menu is active. Bits 1 through 31 are reserved.</param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern int ToUnicode(
            uint wVirtKey, 
            uint wScanCode, 
            byte[] lpKeyState,
            [Out, MarshalAs(UnmanagedType.LPWStr, SizeParamIndex=4)] 
            StringBuilder pwszBuff, 
            int cchBuff,
            uint wFlags);

        [DllImport("user32.dll")]
        public static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, MapType uMapType);


        /// <summary>The set of valid MapTypes used in MapVirtualKey
        /// </summary>
        /// <remarks></remarks>
        public enum MapType : uint
        {
            /// <summary>uCode is a virtual-key code and is translated into a scan code.
            /// If it is a virtual-key code that does not distinguish between left- and
            /// right-hand keys, the left-hand scan code is returned.
            /// If there is no translation, the function returns 0.
            /// </summary>
            /// <remarks></remarks>
            MAPVK_VK_TO_VSC = 0x0,

            /// <summary>uCode is a scan code and is translated into a virtual-key code that
            /// does not distinguish between left- and right-hand keys. If there is no
            /// translation, the function returns 0.
            /// </summary>
            /// <remarks></remarks>
            MAPVK_VSC_TO_VK = 0x1,

            /// <summary>uCode is a virtual-key code and is translated into an unshifted
            /// character value in the low-order word of the return value. Dead keys (diacritics)
            /// are indicated by setting the top bit of the return value. If there is no
            /// translation, the function returns 0.
            /// </summary>
            /// <remarks></remarks>
            MAPVK_VK_TO_CHAR = 0x2,

            /// <summary>Windows NT/2000/XP: uCode is a scan code and is translated into a
            /// virtual-key code that distinguishes between left- and right-hand keys. If
            /// there is no translation, the function returns 0.
            /// </summary>
            /// <remarks></remarks>
            MAPVK_VSC_TO_VK_EX = 0x3,

            /// <summary>Not currently documented
            /// </summary>
            /// <remarks></remarks>
            MAPVK_VK_TO_VSC_EX = 0x4,
        }



    }
}
