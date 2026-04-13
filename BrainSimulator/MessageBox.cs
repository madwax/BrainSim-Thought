using Avalonia.Controls;
using Avalonia.Threading;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Threading.Tasks;

#pragma warning disable CA2007

namespace BrainSimulator
{
    internal class MessageBox
    {
        public enum Buttons 
        {
            Cancel = 0,
            Ok = 1,
            Yes = 2,
            No = 3,
        }

        /// <summary>
        /// Your basic Ok button message box
        /// </summary>
        /// <param name="message"></param>
        /// <param name="title"></param>
        public static async void Alert( string message, string title)
        {
            var theMsgBox = MessageBoxManager.GetMessageBoxStandard( title, message, ButtonEnum.Ok );
            // don't care about the return
            await theMsgBox.ShowAsync();
        }

        public static async Task<MessageBox.Buttons> YesNoCancel( string message, string title )
        {
            var theMsgBox = MessageBoxManager.GetMessageBoxStandard( message, title, ButtonEnum.YesNoCancel );
            ButtonResult buttonIs = await theMsgBox.ShowWindowAsync();

            if( buttonIs == ButtonResult.Yes )
            {
                return MessageBox.Buttons.Yes;
            }
            else if( buttonIs == ButtonResult.No )
            {
                return MessageBox.Buttons.No;
            }
            return MessageBox.Buttons.Cancel;
        }



    }
}
 