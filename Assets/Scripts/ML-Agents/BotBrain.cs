using System;
using UnityEngine;

[Serializable]
public class BotBrain
{
    public float[] weightsInputToHidden;
    public float[] weightsHiddenToOutput;
    public float[] biasHidden;
    public float[] biasOutput;

    public int inputSize;
    public int hiddenSize;
    public int outputSize;

    public BotBrain(int inputSize, int hiddenSize, int outputSize)
    {
        this.inputSize = inputSize;
        this.hiddenSize = hiddenSize;
        this.outputSize = outputSize;

        weightsInputToHidden = new float[inputSize * hiddenSize];
        weightsHiddenToOutput = new float[hiddenSize * outputSize];
        biasHidden = new float[hiddenSize];
        biasOutput = new float[outputSize];

        Randomize();
    }

    public void Randomize()
    {
        InitializeWeights(weightsInputToHidden);
        InitializeWeights(weightsHiddenToOutput);
        InitializeWeights(biasHidden);
        InitializeWeights(biasOutput);
    }

    private void InitializeWeights(float[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            array[i] = UnityEngine.Random.Range(-1f, 1f);
        }
    }

    public float[] Predict(float[] inputs)
    {
        if (inputs == null || inputs.Length < inputSize)
        {
            return new float[outputSize];
        }

        float[] hidden = new float[hiddenSize];
        for (int i = 0; i < hiddenSize; i++)
        {
            float sum = biasHidden[i];
            for (int j = 0; j < inputSize; j++)
            {
                sum += inputs[j] * weightsInputToHidden[j * hiddenSize + i];
            }
            hidden[i] = (float)Math.Tanh(sum);
        }

        float[] outputs = new float[outputSize];
        for (int i = 0; i < outputSize; i++)
        {
            float sum = biasOutput[i];
            for (int j = 0; j < hiddenSize; j++)
            {
                sum += hidden[j] * weightsHiddenToOutput[j * outputSize + i];
            }
            outputs[i] = (float)Math.Tanh(sum);
        }

        return outputs;
    }

    public void Mutate(float rate, float strength)
    {
        MutateArray(weightsInputToHidden, rate, strength);
        MutateArray(weightsHiddenToOutput, rate, strength);
        MutateArray(biasHidden, rate, strength);
        MutateArray(biasOutput, rate, strength);
    }

    private void MutateArray(float[] array, float rate, float strength)
    {
        for (int i = 0; i < array.Length; i++)
        {
            if (UnityEngine.Random.value < rate)
            {
                array[i] += UnityEngine.Random.Range(-strength, strength);
            }
        }
    }

    public BotBrain Clone()
    {
        BotBrain clone = new BotBrain(inputSize, hiddenSize, outputSize);
        Array.Copy(weightsInputToHidden, clone.weightsInputToHidden, weightsInputToHidden.Length);
        Array.Copy(weightsHiddenToOutput, clone.weightsHiddenToOutput, weightsHiddenToOutput.Length);
        Array.Copy(biasHidden, clone.biasHidden, biasHidden.Length);
        Array.Copy(biasOutput, clone.biasOutput, biasOutput.Length);
        return clone;
    }
}
