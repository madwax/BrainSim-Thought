using Avalonia.Controls;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
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
        public static void Alert( string message, string title)
        {
            var theMsgBox = MessageBoxManager.GetMessageBoxStandard( message, title, ButtonEnum.Ok );
            // don't care about the return
            Task.Run( async () => await theMsgBox.ShowAsync() );
        }

        public static MessageBox.Buttons YesNoCancel( string message, string title )
        {
            var theMsgBox = MessageBoxManager.GetMessageBoxStandard( message, title, ButtonEnum.YesNoCancel );

            var results = Task.Run( async () => await theMsgBox.ShowAsync() );
            if( results is not null )
            {
                if( results.Result == ButtonResult.Yes )
                {
                    return MessageBox.Buttons.Yes;
                }
                else if( results.Result == ButtonResult.No )
                {
                    return MessageBox.Buttons.No;
                }
            }
            return MessageBox.Buttons.Cancel;
        }



    }
}
 