using System;
using System.Windows.Input;

namespace 网易云音乐下载.Commands
{
    /// <summary>
    /// 通用命令类，实现 ICommand 接口
    /// 用于 MVVM 模式中的命令绑定
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        /// <summary>
        /// 当 CanExecute 状态改变时触发的事件
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// 创建一个新的 RelayCommand
        /// </summary>
        /// <param name="execute">要执行的 Action</param>
        public RelayCommand(Action<object> execute)
            : this(execute, null)
        {
        }

        /// <summary>
        /// 创建一个新的 RelayCommand，带 CanExecute 判断
        /// </summary>
        /// <param name="execute">要执行的 Action</param>
        /// <param name="canExecute">判断是否可以执行的 Predicate</param>
        public RelayCommand(Action<object> execute, Predicate<object> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 判断命令是否可以执行
        /// </summary>
        public bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke(parameter) ?? true;
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// 手动触发 CanExecuteChanged 事件，强制刷新命令状态
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>
    /// 泛型版本的 RelayCommand，支持强类型参数
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<T> execute)
            : this(execute, null)
        {
        }

        public RelayCommand(Action<T> execute, Predicate<T> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_canExecute == null)
                return true;

            if (parameter == null && typeof(T).IsValueType)
                return false;

            return _canExecute((T)parameter);
        }

        public void Execute(object parameter)
        {
            _execute((T)parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
