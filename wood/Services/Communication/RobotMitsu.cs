using System;
using System.Collections.Generic;
using System.Text;
using Melfa.Robot;
namespace wood.Services.Communication
{
    public class RobotMitsu
    {
        public void Connect()
        {
            MelfaRobot robot = new MelfaRobot("192.168.0.1",3000);
            robot.Connect();

            //("192.168.0.20", 10001);



        }
    }
}
