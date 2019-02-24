// ==++==
//
//   Copyright (c#) GREE Corporation.  All rights reserved.
//
// ==--==
//
// <OWNER>yxj</OWNER>
/*===================================================================================
 **
 ** Class:
 **
 **
 ** Purpose: Class for creating and managing process threads.
 **
 **
 ==================================================================================*/


using System;
using System.Threading;
using System.Windows.Forms;

namespace Controller
{

    /// <summary>
    /// enum indicates the controller threads status 
    /// </summary>
    internal enum eWorkState
    {
        /// <summary>
        /// initialized in constructor
        /// </summary>
        INITIAL = 0,
        /// <summary>
        /// application  works now
        /// </summary>
        WORKING = 1,
        /// <summary>
        /// work had been interupted by pressing pause button
        /// </summary>
        PAUSED = 2,
        /// <summary>
        /// paused thread will release semaphore when continue state set by pressing continue button
        /// </summary>
        CONTINUED = 3,
        /// <summary>
        /// working thread, sleeping thread and paused thread was all over
        /// </summary>
        ABORTED = 4
    }

    /// <summary>
    /// enum switch work thread between working and sleeping, and do work properly.
    /// </summary>
    internal enum eWorkThreadSwitch
    {
        /// <summary>
        /// initialized in constructor
        /// </summary>
        INITIAL = 0,
        /// <summary>
        /// when print thread created or work button printed, working thread switch state must set as TOPWORKTHREAD to start from working thread
        /// </summary>
        TOWORKTHREAD = 1,
        /// <summary>
        /// everytime when work thread do a piece of  work, it should be switch to sleep thread to wait for code finish
        /// </summary>
        TOSLEEPTHREAD = 2,
        /// <summary>
        /// work over or stop by stop button
        /// </summary>
        ENDED = 3
    }

    /// <summary>
    /// shared resource by four thread: working thread, sleeping thread, paused thread, stop thread. (and stop thread)
    /// </summary>
    internal struct WorkStateAndSignal
    {
        /// <summary>
        /// work count interval
        /// </summary>
        public int WorkIndex;
        /// <summary>
        /// switch work state between working thread and sleeping thread
        /// </summary>
        public Semaphore SendWorkOrSleep;
        /// <summary>
        /// switch work thread to pause, continue or stop
        /// </summary>
        public Semaphore SendPauseOrContinue;
        /// <summary>
        /// all work thread state
        /// </summary>
        public eWorkState WorkStatus;
        /// <summary>
        /// working thread and sleeping thread states
        /// </summary>
        public eWorkThreadSwitch SwitchState;
    }

    internal sealed class yWorkThreadHandle
    {
        #region ===========Private Fields================================================================================

        /// <summary>
        /// 总计数
        /// </summary>
        private int WorkNum = 1;
        /// <summary>
        /// 每套份数
        /// </summary>
        private int WorkCountPer = 1;
        /// <summary>
        /// sleeping time sleep time
        /// </summary>
        private int SleepTime;
        /// <summary>
        /// 用来显示线程信息的委托
        /// </summary>
        /// <param name="message"></param>
        private delegate void ShowThreadState(string message);
        /// <summary>
        /// 显示线程信息的控件
        /// </summary>
        private System.Windows.Forms.RichTextBox rtbShowMessage = null;
        /// <summary>
        /// 线程状态、信号量
        /// </summary>
        private WorkStateAndSignal StateAndSignal;
        /// <summary>
        /// work method
        /// </summary>
        private Action<int> WorkAction;
        /// <summary>
        /// show alert message method
        /// </summary>
        private Func<string, System.Windows.Forms.DialogResult> AlertMessage;
        /// <summary>
        /// for check thread method
        /// </summary>
        private Func<int, bool> CheckMethod;

        private Action<int> LastThingToDo;

        //private int temp = 0;

        #endregion

        #region ===========constructor==================================================================================

        /// <summary>
        /// Constructor for work thread control
        /// </summary>
        /// <param name="worktMethod">describe print method in constructor parameter, and this delegate's parameter represent working count index</param>
        /// <param name="checkMethod">check method, first parameter is work index, second is the result of checking thread</param>
        /// <param name="workCount">work count</param>
        /// <param name="workCountPer">per set</param>
        /// <param name="sleepTime">sleep time after every code was over</param>
        /// <param name="rtb">container for show work threads thread states</param>
        /// <param name="alertMessage">describe alert message method, like messagebox's show method.</param>
        public yWorkThreadHandle(Action<int> workMethod,
                                Func<int, bool> checkMethod,
                                Action<int> doLastThing,
                                int workCount,
                                int workCountPer,
                                int sleepTime,
                                System.Windows.Forms.RichTextBox rtb = null,
                                Func<string, System.Windows.Forms.DialogResult> alertMessage = null)
        {
            if (alertMessage == null)
            {
                AlertMessage = System.Windows.Forms.MessageBox.Show;
            }
            else
            {
                AlertMessage = alertMessage;
            }

            if (workCount < 1 || workCountPer < 1)
            {
                AlertMessage("parameters invalid！！");
            }
            else
            {
                WorkNum = workCount;
                WorkCountPer = workCountPer;
            }
            WorkAction = workMethod;
            LastThingToDo = doLastThing;
            CheckMethod = checkMethod;
            if (!initState())
            {
                AlertMessage("内部出问题了！！！");
            }
            SleepTime = sleepTime;
            rtbShowMessage = rtb;

            StateAndSignal.WorkStatus = eWorkState.INITIAL;
        }

        #endregion

        #region ===========Public Interface Methods=====================================================================

        /// <summary>
        /// create a work thread and a sleep thread, and detect if there was a work thread existed
        /// </summary>
        public bool Work_Thread()
        {
            if (StateAndSignal.WorkStatus == eWorkState.WORKING || StateAndSignal.WorkStatus == eWorkState.PAUSED)
            {
                AlertMessage("正在工作!!!!");
            }
            else if (StateAndSignal.WorkStatus == eWorkState.ABORTED || StateAndSignal.SwitchState == eWorkThreadSwitch.ENDED)
            {
                return false;
            }
            else
            {
                StateAndSignal.WorkStatus = eWorkState.WORKING;
                StateAndSignal.SwitchState = eWorkThreadSwitch.TOWORKTHREAD;
                Thread tdWork = new Thread(workThreadFunc);
                tdWork.Priority = ThreadPriority.Normal;
                Thread tdPauser = new Thread(SleepThreadFunc);
                tdPauser.Priority = ThreadPriority.Normal;
                tdWork.Start();
                tdPauser.Start();
            }
            return true;
        }

        /// <summary>
        /// create a pause thread, paused until continue or stop button was pressed
        /// </summary>
        public void Pauser_Thread()
        {
            if (StateAndSignal.WorkStatus == eWorkState.WORKING)
            {
                Thread tdPaused = new Thread(PauseFunc);
                tdPaused.Priority = ThreadPriority.AboveNormal;
                tdPaused.Start();
            }
        }

        public void Continue_Thread()
        {
            if (StateAndSignal.WorkStatus == eWorkState.PAUSED)
            {
                Thread tdContinue = new Thread(ContinueFunc);
                tdContinue.Priority = ThreadPriority.Highest;
                tdContinue.Start();
            }
        }

        public void Stop_Thread()
        {
            if (StateAndSignal.WorkStatus == eWorkState.WORKING || StateAndSignal.WorkStatus == eWorkState.PAUSED)
            {
                Thread tdStoped = new Thread(StopFunc);
                tdStoped.Priority = ThreadPriority.Highest;
                tdStoped.Start();
            }

        }

        public bool Thread_Exit()
        {
            if (StateAndSignal.SwitchState != eWorkThreadSwitch.ENDED && StateAndSignal.WorkStatus != eWorkState.ABORTED)
            {
                return false;
            }

            return true;

        }

        public eWorkState getThreadState()
        {
            return StateAndSignal.WorkStatus;
        }

        /// <summary>
        /// add a line of message in RichTextBox
        /// </summary>
        /// <param name="message">message string</param>
        public void WriteMessageToRichTextBox(string message = ">>")
        {
            if (rtbShowMessage == null)
            {
                return;
            }
            if (this.rtbShowMessage.InvokeRequired)
            {
                ShowThreadState de = new ShowThreadState(WriteMessageToRichTextBox);
                this.rtbShowMessage.Parent.Invoke(de, new object[] { message });
            }
            else
            {
                this.rtbShowMessage.Text += message + "\r\n";
                this.rtbShowMessage.SelectionStart = rtbShowMessage.Text.Length;
                this.rtbShowMessage.ScrollToCaret();            //Now scroll it automatically
            }
        }

        #endregion


        #region ===========Private Methods==============================================================================

        private bool initState()
        {
            if (StateAndSignal.WorkStatus == eWorkState.INITIAL)
            {
                StateAndSignal = new WorkStateAndSignal();
                StateAndSignal.SendWorkOrSleep = new Semaphore(1, 1);
                StateAndSignal.SendPauseOrContinue = new Semaphore(1, 1);
                StateAndSignal.WorkIndex = 1;
                StateAndSignal.WorkStatus = eWorkState.INITIAL;
                StateAndSignal.SwitchState = eWorkThreadSwitch.INITIAL;
                return true;
            }
            return false;
        }


        #region **************Print Thread Methods****************************************


        private void workThreadFunc()
        {
            while (true)
            {
                StateAndSignal.SendWorkOrSleep.WaitOne();    //==================work, sleep, pause threads share

                if (StateAndSignal.SwitchState == eWorkThreadSwitch.TOWORKTHREAD)//保证在工作线程和睡眠线程之间切换
                {
                    WriteMessageToRichTextBox("======work thread");
                    StateAndSignal.SwitchState = eWorkThreadSwitch.TOSLEEPTHREAD;  //switch to sleeping thread after semaphore released

                    if (StateAndSignal.WorkStatus == eWorkState.ABORTED)
                    {
                        StateAndSignal.SendWorkOrSleep.Release();
                        break;
                    }

                    WorkAction(StateAndSignal.WorkIndex);
                    StateAndSignal.WorkIndex++;

                    if (StateAndSignal.WorkIndex == WorkNum + 1)
                    {
                        StateAndSignal.SendWorkOrSleep.Release();
                        break;
                    }
                    //WriteMessageToRichTextBox();
                }

                StateAndSignal.SendWorkOrSleep.Release();    //===================

            }
            WriteMessageToRichTextBox("======work线程结束......");
        }

        private void SleepThreadFunc()
        {
            while (true)
            {
                StateAndSignal.SendWorkOrSleep.WaitOne();  //======================

                if (StateAndSignal.SwitchState == eWorkThreadSwitch.TOSLEEPTHREAD)
                {
                    WriteMessageToRichTextBox("======sleep thread");

                    if (StateAndSignal.WorkStatus == eWorkState.ABORTED)
                    {
                        StateAndSignal.SwitchState = eWorkThreadSwitch.ENDED;
                        StateAndSignal.SendWorkOrSleep.Release();
                        break;
                    }
                    StateAndSignal.SwitchState = eWorkThreadSwitch.TOWORKTHREAD;
                    int index = StateAndSignal.WorkIndex;
                    //checkmethods-----------------------------
                    if (CheckMethod(--index)/*||temp==1*/)
                    {
                        WriteMessageToRichTextBox("Checked........");
                        Thread.Sleep(SleepTime * (WorkCountPer));
                        //temp = 0;
                    }
                    else
                    {
                        WriteMessageToRichTextBox("work出错!!!!!!!!");
                        //temp = 1;
                        if (DialogResult.No == MessageBox.Show("继续work还是重新执行上一套？\r\n点击确认继续work。", "询问", MessageBoxButtons.OKCancel, MessageBoxIcon.Question))
                        {
                            StateAndSignal.WorkIndex = index;
                        }
                        Thread.Sleep(500);
                        Pauser_Thread();
                        Thread.Sleep(500);
                    }
                    if (StateAndSignal.WorkIndex == WorkNum + 1)
                    {
                        StateAndSignal.SwitchState = eWorkThreadSwitch.ENDED;
                        StateAndSignal.WorkStatus = eWorkState.ABORTED;
                        StateAndSignal.SendWorkOrSleep.Release();
                        break;
                    }
                    WriteMessageToRichTextBox();
                }

                StateAndSignal.SendWorkOrSleep.Release();  //=======================
            }
            if (LastThingToDo != null)
            {
                LastThingToDo(StateAndSignal.WorkIndex - 1);
            }
            WriteMessageToRichTextBox("======睡眠线程结束........");
        }

        private void PauseFunc()
        {
            if (StateAndSignal.WorkIndex == WorkNum)
            {
                return;
            }

            if (StateAndSignal.WorkStatus == eWorkState.WORKING)
            {
                StateAndSignal.SendWorkOrSleep.WaitOne();
                StateAndSignal.WorkStatus = eWorkState.PAUSED;
                StateAndSignal.SwitchState = eWorkThreadSwitch.TOWORKTHREAD;
                WriteMessageToRichTextBox("======pausing thread");
                WriteMessageToRichTextBox("按继续按钮继续work..........");
                while (true)
                {
                    StateAndSignal.SendPauseOrContinue.WaitOne();   //====================
                    if (StateAndSignal.WorkStatus == eWorkState.ABORTED)
                    {
                        WriteMessageToRichTextBox("======暂停线程结束........");
                        StateAndSignal.SendPauseOrContinue.Release();
                        StateAndSignal.SendWorkOrSleep.Release();
                        break;
                    }
                    else if (StateAndSignal.WorkStatus == eWorkState.CONTINUED)
                    {
                        StateAndSignal.WorkStatus = eWorkState.WORKING;
                        WriteMessageToRichTextBox("======暂停线程继续work........");
                        StateAndSignal.SendPauseOrContinue.Release();
                        StateAndSignal.SendWorkOrSleep.Release();
                        break;
                    }
                    Thread.Sleep(200);
                    StateAndSignal.SendPauseOrContinue.Release();   //=====================
                }
            }
        }

        private void ContinueFunc()
        {
            if (StateAndSignal.WorkStatus == eWorkState.PAUSED)
            {
                StateAndSignal.SendPauseOrContinue.WaitOne();   //====================
                StateAndSignal.WorkStatus = eWorkState.CONTINUED;
                WriteMessageToRichTextBox("======continue thread");
                Thread.Sleep(200);
                WriteMessageToRichTextBox("---------------------------返回暂停线程........");
                StateAndSignal.SendPauseOrContinue.Release();   //====================
            }
        }

        private void StopFunc()
        {
            if (StateAndSignal.WorkStatus == eWorkState.PAUSED)
            {
                StateAndSignal.SendPauseOrContinue.WaitOne();   //===============
                WriteMessageToRichTextBox("======stop thread");
                WriteMessageToRichTextBox("OVER!!!!!!........");
                WriteMessageToRichTextBox("---------------------------终止线程结束........");
                StateAndSignal.WorkStatus = eWorkState.ABORTED;
                //Thread.Sleep(200);
                StateAndSignal.SendPauseOrContinue.Release();   //===============
            }
            else if (StateAndSignal.WorkStatus == eWorkState.WORKING)
            {
                StateAndSignal.SendWorkOrSleep.WaitOne();
                StateAndSignal.SendPauseOrContinue.WaitOne();   //===============
                WriteMessageToRichTextBox("OVER!!!!!!........");
                WriteMessageToRichTextBox("----------------------------终止线程结束........");
                //Thread.Sleep(200);
                StateAndSignal.SwitchState = eWorkThreadSwitch.TOWORKTHREAD;
                StateAndSignal.WorkStatus = eWorkState.ABORTED;
                StateAndSignal.SendPauseOrContinue.Release();   //===============
                StateAndSignal.SendWorkOrSleep.Release();

            }
        }

        #endregion

        #endregion
    }
}
