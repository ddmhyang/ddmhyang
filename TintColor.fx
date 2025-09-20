// TintColor.fx

// C# �ڵ忡�� ä���� ���� (Parameter)
float4 TintColor : register(C0);

// WPF���� �ؽ�ó�� ���޹ޱ� ���� ���÷�
sampler2D implicitInput : register(S0);

// �ȼ� ���̴� ���� �Լ�
float4 main(float2 uv : TEXCOORD) : COLOR
{
    // 1. ���� �ؽ�ó(ȸ���� �Ӹ� �̹���)���� ���� �ȼ��� ������ �����ɴϴ�.
    float4 originalColor = tex2D(implicitInput, uv);
    
    // 2. ���� ���� TintColor�� ���մϴ�.
    //    ȸ���� �̹����� R,G,B ���� ��� �����Ƿ�, TintColor�� ���ϸ� �ش� ������ �ڿ������� ����ϴ�.
    float4 outputColor = originalColor * TintColor;
    
    // 3. ������ ����(Alpha)�� �״�� �����մϴ�.
    outputColor.a = originalColor.a;

    return outputColor;
}
