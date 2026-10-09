using NUnit.Framework;
using UnityEngine;

/// <summary>
/// ActuatorCommands frame decoding and dot matrix content rules.
/// </summary>
public class ActuatorCommandsTests
{
    [Test]
    public void DecodeMatrix_MapsCornersRowMajorMsbFirst()
    {
        byte[] packet = new byte[1 + ActuatorCommands.MatrixFrameBytes];
        packet[1] = 0x80;
        packet[ActuatorCommands.MatrixFrameBytes] = 0x01;

        bool[,] pixels = ActuatorCommands.DecodeMatrix(packet, 1);

        Assert.IsTrue(pixels[0, 0]);
        Assert.IsTrue(pixels[7, 23]);
        int lit = 0;
        foreach (bool pixel in pixels)
        {
            lit += pixel ? 1 : 0;
        }
        Assert.AreEqual(2, lit);
    }

    [Test]
    public void DecodeMatrix_Checkerboard()
    {
        byte[] packet = new byte[ActuatorCommands.MatrixFrameBytes];
        for (int r = 0; r < ActuatorCommands.MatrixRows; r++)
        {
            for (int c = 0; c < ActuatorCommands.MatrixColumns; c++)
            {
                if ((r + c) % 2 == 0)
                {
                    int i = r * ActuatorCommands.MatrixColumns + c;
                    packet[i / 8] |= (byte)(0x80 >> (i % 8));
                }
            }
        }

        bool[,] pixels = ActuatorCommands.DecodeMatrix(packet, 0);

        for (int r = 0; r < ActuatorCommands.MatrixRows; r++)
        {
            for (int c = 0; c < ActuatorCommands.MatrixColumns; c++)
            {
                Assert.AreEqual((r + c) % 2 == 0, pixels[r, c], $"pixel ({r}, {c})");
            }
        }
    }

    [Test]
    public void DecodeLeds_ReadsRgbTriplets()
    {
        byte[] packet = new byte[1 + ActuatorCommands.LedFrameBytes];
        packet[1] = 10;
        packet[2] = 20;
        packet[3] = 30;
        packet[ActuatorCommands.LedFrameBytes - 2] = 1;
        packet[ActuatorCommands.LedFrameBytes - 1] = 2;
        packet[ActuatorCommands.LedFrameBytes] = 3;

        Color32[] colors = ActuatorCommands.DecodeLeds(packet, 1);

        Assert.AreEqual(ActuatorCommands.LedCount, colors.Length);
        Assert.AreEqual(new Color32(10, 20, 30, 255), colors[0]);
        Assert.AreEqual(new Color32(1, 2, 3, 255), colors[ActuatorCommands.LedCount - 1]);
    }

    [Test]
    public void DecodeText_ReadsLengthPrefix()
    {
        byte[] packet = { 34, 3, (byte)'A', (byte)'B', (byte)'C', (byte)'D' };
        Assert.AreEqual("ABC", ActuatorCommands.DecodeText(packet, 1));
    }

    [Test]
    public void LatestMatrixCallWins()
    {
        ActuatorCommands commands = new ActuatorCommands();
        Assert.AreEqual(ActuatorCommands.MatrixContent.None, commands.Matrix);

        commands.SetMatrix(new bool[ActuatorCommands.MatrixRows, ActuatorCommands.MatrixColumns]);
        commands.SetText("HI");
        Assert.AreEqual(ActuatorCommands.MatrixContent.Text, commands.Matrix);
        Assert.IsNull(commands.GetMatrixPixels());

        commands.SetMatrix(new bool[ActuatorCommands.MatrixRows, ActuatorCommands.MatrixColumns]);
        Assert.AreEqual(ActuatorCommands.MatrixContent.Pixels, commands.Matrix);
        Assert.IsNull(commands.GetMatrixText());

        commands.SetText("");
        Assert.AreEqual(ActuatorCommands.MatrixContent.None, commands.Matrix);
    }

    [Test]
    public void Clear_ReturnsToIdle()
    {
        ActuatorCommands commands = new ActuatorCommands();
        commands.SetText("HI");
        commands.SetLeds(new Color32[ActuatorCommands.LedCount]);

        commands.Clear();

        Assert.AreEqual(ActuatorCommands.MatrixContent.None, commands.Matrix);
        Assert.IsFalse(commands.HasLeds);
    }
}
