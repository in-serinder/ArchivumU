using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace ArchivumU.Models
{
    public static class DialogMgrModel
    {

        private static Window _mainWindow;
        private static object _mainWindowDataContext; 
        private static bool _hasActiveDialog;

        /// <summary>
        /// 初始化对话框管理器
        /// </summary>
        /// <param name="mainWindow">主窗口实例</param>
        public static void Initialize(Window mainWindow)
        {
            _mainWindow = mainWindow;
            _mainWindowDataContext = mainWindow.DataContext;
            // 监听主窗口关闭事件，当有弹窗时阻止关闭
            _mainWindow.Closing += (sender, e) =>
            {
                if (_hasActiveDialog)
                {
                    e.Cancel = true;
                }
            };
        }
        
        public static T GetMainViewModel<T>() where T : class
        {
            return _mainWindowDataContext as T;
        }

        #region 推荐：异步版本（避免死锁）

        /// <summary>
        /// 异步显示对话框（推荐）
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <returns>任务</returns>
        public static async Task ShowDialogAsync<T>() where T : Window, new()
        {
            await ShowDialogAsync(new T());
        }

        /// <summary>
        /// 异步显示对话框（推荐）
        /// </summary>
        /// <param name="window">窗口实例</param>
        /// <returns>任务</returns>
        public static async Task ShowDialogAsync(Window window)
        {
            ValidateMainWindow();
            
            _hasActiveDialog = true;
            try
            {
                await window.ShowDialog(_mainWindow);
            }
            finally
            {
                _hasActiveDialog = false;
            }
        }

        /// <summary>
        /// 异步显示对话框并返回结果（推荐）
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <returns>对话框返回结果</returns>
        public static async Task<TResult> ShowDialogAsync<T, TResult>() where T : Window, new()
        {
            return await ShowDialogAsync<TResult>(new T());
        }

        /// <summary>
        /// 异步显示对话框并返回结果（推荐）
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="window">窗口实例</param>
        /// <returns>对话框返回结果</returns>
        public static async Task<TResult> ShowDialogAsync<TResult>(Window window)
        {
            ValidateMainWindow();
            
            _hasActiveDialog = true;
            try
            {
                return await window.ShowDialog<TResult>(_mainWindow);
            }
            finally
            {
                _hasActiveDialog = false;
            }
        }

        #endregion

        #region 同步版本（仅在非 UI 线程使用）

        /// <summary>
        /// 同步显示对话框（仅在非 UI 线程使用）
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        public static void ShowDialog<T>() where T : Window, new()
        {
            ShowDialog(new T());
        }

        /// <summary>
        /// 同步显示对话框（仅在非 UI 线程使用）
        /// </summary>
        /// <param name="window">窗口实例</param>
        public static void ShowDialog(Window window)
        {
            ValidateMainWindow();
            
            _hasActiveDialog = true;
            try
            {
                // 使用 ConfigureAwait(false) 避免死锁
                window.ShowDialog(_mainWindow).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            finally
            {
                _hasActiveDialog = false;
            }
        }

        /// <summary>
        /// 同步显示对话框并返回结果（仅在非 UI 线程使用）
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <returns>对话框返回结果</returns>
        public static TResult ShowDialog<T, TResult>() where T : Window, new()
        {
            return ShowDialog<TResult>(new T());
        }

        /// <summary>
        /// 同步显示对话框并返回结果（仅在非 UI 线程使用）
        /// </summary>
        /// <typeparam name="TResult">返回结果类型</typeparam>
        /// <param name="window">窗口实例</param>
        /// <returns>对话框返回结果</returns>
        public static TResult ShowDialog<TResult>(Window window)
        {
            ValidateMainWindow();
            
            _hasActiveDialog = true;
            try
            {
                return window.ShowDialog<TResult>(_mainWindow).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            finally
            {
                _hasActiveDialog = false;
            }
        }

        #endregion

        /// <summary>
        /// 验证主窗口是否已初始化
        /// </summary>
        private static void ValidateMainWindow()
        {
            if (_mainWindow == null)
                throw new InvalidOperationException("DialogMgrModel 尚未初始化，请先调用 Initialize 方法");
        }
    }
}