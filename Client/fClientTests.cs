using Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Net.Sockets;

namespace Client
{
    [TestClass]
    public class fClientTests
    {
        private MockRepository mockRepository;



        [TestInitialize]
        public void TestInitialize()
        {
            this.mockRepository = new MockRepository(MockBehavior.Strict);


        }

        private fClient CreatefClient()
        {
            return new fClient();
        }

        [TestMethod]
        public void GetState_StateUnderTest_ExpectedBehavior()
        {
            // Arrange
            var fClient = this.CreatefClient();
            TcpClient tcpClient = null;

            // Act
            var result = fClient.GetState(
                tcpClient);

            // Assert
            Assert.Fail();
            this.mockRepository.VerifyAll();
        }
    }
}
