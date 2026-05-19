import { useGetAllSubscriptionsQuery } from "../store/services/subscription";

export function useSubscriptionPage() {
    const { data, isLoading, error } = useGetAllSubscriptionsQuery();
    const subscriptions = data?.value;

    return { subscriptions, isLoading, error };
}
