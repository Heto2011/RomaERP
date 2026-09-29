using Amazon;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using RomaERP.Application.Common.Interfaces;

namespace RomaERP.Infrastructure.HR;

/// <summary>Compares an attendance selfie against an employee's reference photo using AWS Rekognition's
/// CompareFaces API — real face verification, not a simulated true/false toggle. Inactive until
/// Aws:Rekognition:AccessKeyId and Aws:Rekognition:SecretAccessKey are set in configuration, so
/// attendance keeps working on GPS geofencing alone until an AWS account is connected.</summary>
public class AwsRekognitionFaceVerificationProvider : IFaceVerificationProvider
{
    private readonly string? _accessKeyId;
    private readonly string? _secretAccessKey;
    private readonly string _region;
    private readonly float _similarityThreshold;

    public AwsRekognitionFaceVerificationProvider(IConfiguration configuration)
    {
        _accessKeyId = configuration["Aws:Rekognition:AccessKeyId"];
        _secretAccessKey = configuration["Aws:Rekognition:SecretAccessKey"];
        _region = configuration["Aws:Rekognition:Region"] ?? "eu-west-1";
        _similarityThreshold = float.TryParse(configuration["Aws:Rekognition:SimilarityThreshold"], out var threshold) ? threshold : 80f;
    }

    public string Name => "AWS Rekognition";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_accessKeyId) && !string.IsNullOrWhiteSpace(_secretAccessKey);

    public async Task<FaceVerificationResult> CompareFacesAsync(byte[] referenceImageBytes, byte[] candidateImageBytes, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new FaceVerificationResult(false, false, null,
                "AWS Rekognition غير مفعّل — لسه مفيش Aws:Rekognition:AccessKeyId و Aws:Rekognition:SecretAccessKey في الإعدادات.");

        using var client = new AmazonRekognitionClient(
            new BasicAWSCredentials(_accessKeyId, _secretAccessKey),
            RegionEndpoint.GetBySystemName(_region));

        using var referenceStream = new MemoryStream(referenceImageBytes);
        using var candidateStream = new MemoryStream(candidateImageBytes);

        var request = new CompareFacesRequest
        {
            SourceImage = new Image { Bytes = referenceStream },
            TargetImage = new Image { Bytes = candidateStream },
            SimilarityThreshold = _similarityThreshold
        };

        try
        {
            var response = await client.CompareFacesAsync(request, ct);
            var bestMatch = response.FaceMatches
                .OrderByDescending(m => m.Similarity)
                .FirstOrDefault();

            if (bestMatch?.Similarity is not { } similarity)
                return new FaceVerificationResult(true, false, null, "الوجه في الصورة مش مطابق للصورة المرجعية.");

            return new FaceVerificationResult(true, true, (decimal)similarity, null);
        }
        catch (InvalidParameterException)
        {
            return new FaceVerificationResult(false, false, null, "مفيش وجه واضح في إحدى الصورتين — حاول تاني في إضاءة أحسن.");
        }
        catch (Exception ex)
        {
            return new FaceVerificationResult(false, false, null, ex.Message);
        }
    }
}
