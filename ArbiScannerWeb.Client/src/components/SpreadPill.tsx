interface SpreadPillProps {
    spreadPercent: number;
}

export default function SpreadPill({ spreadPercent }: Readonly<SpreadPillProps>) {
    const positive = spreadPercent >= 0;

    return (
        <span
            className={`inline-flex items-center text-sm font-medium px-3 py-1 rounded-full ${
                positive
                    ? 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400'
                    : 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400'
            }`}
        >
            {spreadPercent.toFixed(2)}%
        </span>
    );
}
