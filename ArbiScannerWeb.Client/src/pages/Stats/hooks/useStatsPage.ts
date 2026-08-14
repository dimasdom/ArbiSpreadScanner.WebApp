import { useMemo, useState } from 'react';
import { useGetLatestStatsQuery, useGetSnapshotIndexQuery, useGetStatsByIdQuery } from '../../../store/services/stats';

export function useStatsPage() {
    const [selectedId, setSelectedId] = useState<string | null>(null);

    const { data: indexResult, isLoading: isIndexLoading } = useGetSnapshotIndexQuery();
    const { data: latestResult, isLoading: isLatestLoading, isError: isLatestError } = useGetLatestStatsQuery(undefined, {
        skip: selectedId !== null,
    });
    const { data: byIdResult, isLoading: isByIdLoading, isError: isByIdError } = useGetStatsByIdQuery(selectedId ?? '', {
        skip: selectedId === null,
    });

    // Newest first — matches the order the API already returns.
    const snapshots = useMemo(() => indexResult?.value ?? [], [indexResult]);

    const snapshot = selectedId ? byIdResult?.value : latestResult?.value;
    const isLoading = isIndexLoading || (selectedId ? isByIdLoading : isLatestLoading);
    const isError = selectedId ? isByIdError : isLatestError;

    // While viewing "latest", the select should visually point at the newest record.
    const selectedSnapshotId = selectedId ?? snapshots[0]?.id ?? '';

    const handleSnapshotChange = (id: string) => {
        setSelectedId(id || null);
    };

    return {
        snapshot,
        isLoading,
        isError,
        hasAnySnapshot: snapshots.length > 0,
        snapshots,
        selectedSnapshotId,
        isViewingLatest: selectedId === null,
        handleSnapshotChange,
    };
}
