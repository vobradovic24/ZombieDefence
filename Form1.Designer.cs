namespace ZombieDefence
{
    partial class GameForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            components = new System.ComponentModel.Container();
            zombieTimer = new System.Windows.Forms.Timer(components);
            SuspendLayout();
            // 
            // zombieTimer
            // 
            zombieTimer.Interval = 500;
            zombieTimer.Tick += zombieTimer_Tick;
            // 
            // GameForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            DoubleBuffered = true;
            Name = "GameForm";
            Text = "Form1";
            FormClosing += GameForm_FormClosing;
            Load += GameForm_Load;
            Paint += Form1_Paint;
            Resize += Form1_Resize;
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Timer zombieTimer;
    }
}
