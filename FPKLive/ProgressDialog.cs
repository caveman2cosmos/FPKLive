
# region Heading

/**************************************************************************************************************/
/*                                                                                                            */
/*  ProgressDialog.cs                                                                                         */
/*                                                                                                            */
/*  Implements a dialog to spawn a thread to execute some long-running code                                   */
/*                                                                                                            */
/*  This is free code, use it as you require. It was a good learning exercise for me and I hope it will be    */
/*  for you too. If you modify it please use your own namespace.                                              */
/*                                                                                                            */
/*  If you like it or have suggestions for improvements please let me know at: PIEBALDconsult@aol.com         */
/*                                                                                                            */
/*  Modification history:                                                                                     */
/*  2006/05/16          Sir John E. Boucher     Created                                                       */
/*                                                                                                            */
/**************************************************************************************************************/

# endregion 

using System;
using System.Windows.Forms;

namespace PIEBALD.Dialogs
{
    /** 
    <summary>
        Implements a dialog to spawn a thread to execute some long-running code.
    </summary>
    <remarks>
        Demonstrates safe cross-thread invocation and stuff.
    </remarks>
    */
    public sealed class ProgressDialog : System.Windows.Forms.Form
    {
        public delegate object ProgressDialogStart(params object[] Params);

        private readonly ProgressDialogStart onstart = null;
        private readonly object[] paramlist = null;
        private object result = null;

        /** 
        <summary>
            Constructor.
        </summary>
        <remarks>
            Uses the default icon.
        </remarks>
        <param name="Text">
            Caption for the dialog.
        </param>
        <param name="Style">
            Style for the ProgressBar.
        </param>
        <param name="Cancelable">
            Whether or not to show the Cancel button.
        </param>
        <param name="StartMethod">
            The method to run.
        </param>
        <param name="Params">
            Parameters to pass to the StartMethod when it runs (optional).
        </param>
        <exception cref="System.NullReferenceException">
            Will be thrown if the StartMethod value is null.
        </exception>
        */
        public ProgressDialog(string Text, System.Windows.Forms.ProgressBarStyle Style, bool Cancelable, ProgressDialogStart StartMethod, params object[] Params) : this(Text, null, Style, Cancelable, StartMethod, Params)
        {
        }

        /** 
        <summary>
            Constructor.
        </summary>
        <remarks>
        </remarks>
        <param name="Text">
            Caption for the dialog.
        </param>
        <param name="Style">
            Style for the ProgressBar.
        </param>
        <param name="Cancelable">
            Whether or not to show the Cancel button.
        </param>
        <param name="StartMethod">
            The method to run.
        </param>
        <param name="Params">
            Parameters to pass to the StartMethod when it runs (optional).
        </param>
        <exception cref="System.NullReferenceException">
            Will be thrown if the StartMethod value is null.
        </exception>
        */
        public ProgressDialog(string Text, System.Drawing.Icon Icon, System.Windows.Forms.ProgressBarStyle Style, bool Cancelable, ProgressDialogStart StartMethod, params object[] Params) : base()
        {
            if (StartMethod != null)
            {
                InitializeComponent();

                this.Text = Text;

                if (Icon != null)
                {
                    this.Icon = Icon;
                }

                pbProgress.Style = Style;

                bCancel.Visible = Cancelable;

                onstart = StartMethod;

                paramlist = Params;

                StartPosition = FormStartPosition.CenterScreen;

                //OnUpdateProgress += new UpdateProgress(UpdateProgressHandler);
            }
            else
            {
                throw (new System.NullReferenceException("A start method must be provided"));
            }
        }

        /** 
        <summary>
            Shows the dialog and runs the method.
        </summary>
        <remarks>
        </remarks>
        <returns>
            System.Windows.Forms.DialogResult.OK or System.Windows.Forms.DialogResult.Cancel
        </returns>
        */
        public new System.Windows.Forms.DialogResult ShowDialog()
        {
            base.ShowDialog();

            return ((System.Windows.Forms.DialogResult)bCancel.Tag);
        }

        #region Public properties

        /** 
        <summary>
            Retrieves the result of the method.
        </summary>
        <remarks>
        </remarks>
        <returns>
            The result of the method.
        </returns>
        */
        public object        Result => (result);

        private delegate bool CancelGetDelegate();

        /** 
        <summary>
            Whether or not cancel was requested.
        </summary>
        <remarks>
        </remarks>
        <returns>
            True if a cancel was requested, otherwise true.
        </returns>
        */
        public bool        WasCancelled
        {
            get
            {
                if (InvokeRequired)
                {
                    CancelGetDelegate temp = delegate ()
                    {
                        return (WasCancelled);
                    };

                    return ((bool)Invoke(temp));
                }
                else
                {
                    return
                    (
                        (System.Windows.Forms.DialogResult)bCancel.Tag ==
                            System.Windows.Forms.DialogResult.Cancel
                    );
                }
            }
        }

        #endregion

        #region Private properties

        private delegate System.Windows.Forms.DialogResult StateGetDelegate();
        private delegate void StateSetDelegate(System.Windows.Forms.DialogResult State);

        private System.Windows.Forms.DialogResult        State
        {
            get
            {
                if (InvokeRequired)
                {
                    StateGetDelegate temp = delegate ()
                    {
                        return (State);
                    };

                    return ((System.Windows.Forms.DialogResult)Invoke(temp));
                }
                else
                {
                    return (DialogResult);
                }
            }

            set
            {
                if (InvokeRequired)
                {
                    StateSetDelegate temp = delegate (System.Windows.Forms.DialogResult State)
                    {
                        this.State = value;
                    };

                    Invoke(temp, value);
                }
                else
                {
                    DialogResult = value;
                }
            }
        }

        private delegate int ProgressGetDelegate();
        private delegate void ProgressSetDelegate(int Progress);

        private int Progress
        {
            get
            {
                if (InvokeRequired)
                {
                    ProgressGetDelegate temp = delegate ()
                    {
                        return (pbProgress.Value);
                    };

                    return ((int)Invoke(temp));
                }
                else
                {
                    return (pbProgress.Value);
                }
            }

            set
            {
                if (InvokeRequired)
                {
                    ProgressSetDelegate temp = delegate (int Progress)
                    {
                        pbProgress.Value = value;
                    };

                    Invoke(temp, value);
                }
                else
                {
                    pbProgress.Value = System.Math.Abs(value) % pbProgress.Maximum + 1;
                }
            }
        }

        #endregion

        #region Private event handlers

        private void ProgressDialog_Load
        (
            object sender
        ,
            System.EventArgs e
        )
        {
            bCancel.Tag = System.Windows.Forms.DialogResult.OK;
            bCancel.Enabled = true;
            bCancel.Text = "Cancel";

            pbProgress.Value = pbProgress.Minimum;

            (new System.Threading.Thread
            (
                delegate ()
                {
                    result = onstart(paramlist);

                    State = System.Windows.Forms.DialogResult.OK;
                }
            )).Start();
        }

        private void ProgressDialog_FormClosing
        (
            object sender
        ,
            System.Windows.Forms.FormClosingEventArgs e
        )
        {
            switch (State)
            {
                case System.Windows.Forms.DialogResult.None:
                    {
                        e.Cancel = true;
                        break;
                    }

                case System.Windows.Forms.DialogResult.Cancel:
                    {
                        if (!bCancel.Visible)
                        {
                            System.Windows.Forms.MessageBox.Show
                            (
                                "This operation can't be cancelled"
                            ,
                                "Can't close"
                            ,
                                System.Windows.Forms.MessageBoxButtons.OK
                            ,
                                System.Windows.Forms.MessageBoxIcon.Hand
                            );
                        }
                        else
                        {
                            bCancel.Enabled = false;
                            bCancel.Text = "Cancelling";
                            bCancel.Tag = State;
                        }

                        e.Cancel = true;

                        break;
                    }
            }
        }

        #endregion

        #region UpdateProgress event stuff

       // private event UpdateProgress OnUpdateProgress;

        /** 
        <summary>
            Set the ProgressBar to some percentage done.
        </summary>
        <remarks>
            Used for Block and Continuous style ProgressBars
        </remarks>
        <param name="Percent">
            Specify 0 through 100.
        </param>
       */
        public void RaiseUpdateProgress(int Percent, string Text = null)
        {
            this.BeginInvoke(new Action(() =>
            {
                this.Progress = Percent;
                if (Text != null) this.Text = Text;
            }));
            // OnUpdateProgress(this, new UpdateProgressEventArgs(Percent, Text));
        }

//         private void UpdateProgressHandler(object sender, UpdateProgressEventArgs e)
//         {
//             Progress = e.Percent;
//             if (e.Text != null) Text = e.Text;
//         }

        #endregion

        #region UpdateProgressEventArgs

        private delegate void UpdateProgress(object sender, UpdateProgressEventArgs e);
        private sealed class UpdateProgressEventArgs : System.EventArgs
        {
            private readonly int percent;
            private readonly string text;

            public UpdateProgressEventArgs(int Percent, string Text)
            {
                percent = Percent;
                text = Text;
            }

            public int Percent => (percent);
            public string Text => (text);

            public override string ToString()
            {
                return (percent.ToString());
            }
        }

        #endregion

        #region Windows Form Designer generated code

        private readonly System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            pbProgress = new System.Windows.Forms.ProgressBar();
            bCancel = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // pbProgress
            // 
            pbProgress.Dock = System.Windows.Forms.DockStyle.Top;
            pbProgress.Location = new System.Drawing.Point(0, 0);
            pbProgress.Name = "pbProgress";
            pbProgress.Size = new System.Drawing.Size(294, 23);
            pbProgress.TabIndex = 0;
            // 
            // bCancel
            // 
            bCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            bCancel.Dock = System.Windows.Forms.DockStyle.Bottom;
            bCancel.Enabled = false;
            bCancel.Location = new System.Drawing.Point(0, 22);
            bCancel.Name = "bCancel";
            bCancel.Size = new System.Drawing.Size(294, 23);
            bCancel.TabIndex = 1;
            bCancel.Text = "Cancel";
            bCancel.UseVisualStyleBackColor = true;
            // 
            // ProgressDialog
            // 
            AcceptButton = bCancel;
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = bCancel;
            ClientSize = new System.Drawing.Size(294, 45);
            Controls.Add(bCancel);
            Controls.Add(pbProgress);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "ProgressDialog";
            ShowInTaskbar = false;
            SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "ProgressDialog";
            FormClosing += new System.Windows.Forms.FormClosingEventHandler(ProgressDialog_FormClosing);
            Load += new System.EventHandler(ProgressDialog_Load);
            ResumeLayout(false);
        }

        private System.Windows.Forms.ProgressBar pbProgress;
        private System.Windows.Forms.Button bCancel;

        #endregion

    }
}