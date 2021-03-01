using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using cServer;
using System.Net.Sockets;
using Server.DataModel;

namespace cServer
{
    [TestClass]
    public class ProgramTests
    {
        private MockRepository mockRepository;



        [TestInitialize]
        public void TestInitialize()
        {
            this.mockRepository = new MockRepository(MockBehavior.Strict);


        }

        private Program CreateProgram()
        {
            return new Program();
        }

        [TestMethod]
        public void CheckClientAlive_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var program = this.CreateProgram();

            // Act
            Program.CheckClientAlive();

            // Assert
            Assert.Fail();
            this.mockRepository.VerifyAll();
        }

        [TestMethod]
        public void HandleClients_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var program = this.CreateProgram();
            string cluid = null;

            // Act
            Program.HandleClients(
                cluid);

            // Assert
            Assert.Fail();
            this.mockRepository.VerifyAll();
        }

        [TestMethod]
        public void readCommand_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var program = this.CreateProgram();
            TcpClient client = null;
            string clientid = null;
            byte[] sCommand = null;
            int byte_count = 0;

            // Act
            Program.readCommand(
                client,
                clientid,
                sCommand,
                byte_count);

            // Assert
            Assert.Fail();
            this.mockRepository.VerifyAll();
        }

        [TestMethod]
        public void Sendbuf2Client_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var program = this.CreateProgram();
            TcpClient c = null;
            byte[] buffer = null;

            // Act
            Program.Sendbuf2Client(
                c,
                buffer);

            // Assert
            Assert.Fail();
            this.mockRepository.VerifyAll();
        }

        [TestMethod]
        public void SendOrderBooktoClients_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var program = this.CreateProgram();
            TOrderBook OrderBook = null;
            TcpClient client = null;

            // Act
            Program.SendOrderBooktoClients(
                OrderBook,
                client);

            // Assert
            Assert.Fail();
            this.mockRepository.VerifyAll();
        }

        [TestMethod]
        public void SendTradeHistorytoClients_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var program = this.CreateProgram();
            TTradeHistory TradeBook = null;
            TcpClient client = null;

            // Act
            Program.SendTradeHistorytoClients(
                TradeBook,
                client);

            // Assert
            Assert.Fail();
            this.mockRepository.VerifyAll();
        }

        [TestMethod]
        public void Broadcast2Clients_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var program = this.CreateProgram();
            string msg = null;

            // Act
            Program.Broadcast2Clients(
                msg);

            // Assert
            Assert.Fail();
            this.mockRepository.VerifyAll();
        }

        [TestMethod]
        public void CheckOrderForTrade_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var program = this.CreateProgram();
            TOrder O = null;

            // Act
            var result = Program.CheckOrderForTrade(
                O);

            // Assert
            Assert.Fail();
            this.mockRepository.VerifyAll();
        }

        [TestMethod]
        public void ErrorCatcher_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var program = this.CreateProgram();
            Exception ex = null;
            string eventname = null;

            // Act
            Program.ErrorCatcher(
                ex,
                eventname);

            // Assert
            Assert.Fail();
            this.mockRepository.VerifyAll();
        }
    }
}
