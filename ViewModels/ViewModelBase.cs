using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace 网易云音乐下载.ViewModels
{
    /// <summary>
    /// ViewModel 基类，实现 INotifyPropertyChanged 接口
    /// 为所有 ViewModel 提供属性变更通知功能
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        /// <param name="propertyName">变更的属性名</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 设置属性值并触发变更通知（如果值发生变化）
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">属性字段的引用</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名（自动获取）</param>
        /// <returns>如果值发生变化返回 true，否则返回 false</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            // 使用 EqualityComparer 处理值类型和引用类型的比较
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// 设置属性值并触发变更通知，同时执行额外的操作
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">属性字段的引用</param>
        /// <param name="value">新值</param>
        /// <param name="onChanged">值变化后执行的操作</param>
        /// <param name="propertyName">属性名（自动获取）</param>
        /// <returns>如果值发生变化返回 true，否则返回 false</returns>
        protected bool SetProperty<T>(ref T field, T value, Action onChanged, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            onChanged?.Invoke();
            return true;
        }
    }
}
