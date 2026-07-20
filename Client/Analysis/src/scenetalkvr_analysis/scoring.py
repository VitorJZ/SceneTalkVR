def reverse_score(raw: float, scale_min: float, scale_max: float) -> float:
    return scale_max + scale_min - raw
