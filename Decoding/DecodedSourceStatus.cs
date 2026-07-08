namespace ZCodeBundler.Decoding;

public enum DecodedSourceStatus
{
    Same,
    Different,
    Missing,
    InvalidPath,
    DuplicateTarget,
    SourceReadError
}