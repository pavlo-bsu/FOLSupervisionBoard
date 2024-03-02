using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;

namespace Pavlo.FOLSupervisionBoard.Viemodel
{
    public class CommandRefresh: ICommand
    {
        /// <summary>
        /// VM of the window
        /// </summary>
        protected Viewmodel vm = null;

        public CommandRefresh(Viewmodel vm)
        {
            this.vm = vm;
            this.vm.PropertyChanged += vm_PropertyChanged;
        }

        private void vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (CanExecuteChanged != null)
            {
                //rise check at any property change 
                CanExecuteChanged(this, new EventArgs());
            }
        }

        public event EventHandler CanExecuteChanged;

        public virtual bool CanExecute(object parameter)
        {
            if (vm == null)
            {
                return false;
            }
            else
            {
                if (vm.AvaliableCOMportsSelectedIndex >= 0 && !vm.IsResposeAwaiting && !vm.IsTestGenerOn && !vm.AreUnitsOnLowpowerMode)
                    return true;
                else
                    return false;
            }
        }
        
        public void Execute(object? parameter)
        {
            vm?.RefreshData();
        }
    }
}
