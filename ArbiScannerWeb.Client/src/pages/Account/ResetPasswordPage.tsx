import { useResetPasswordForm } from './hooks/useResetPasswordForm';

export default function ResetPasswordPage() {
    const { fields, errors, loading, setField, handleSubmit } = useResetPasswordForm();

    return (
        <form onSubmit={handleSubmit} className="min-h-[60vh] flex items-center justify-center px-4 py-8">
            <div className="w-full max-w-md">
                <div className="bg-white rounded-2xl shadow-md ring-1 ring-gray-50 ring-inset p-6">
                    <h2 className="text-2xl font-semibold text-gray-800 mb-4">Reset password</h2>

                    <div className="space-y-4">

                        <div>
                            <label htmlFor="password" className="block text-sm font-medium text-gray-700">New password</label>
                            <input
                                type="password"
                                id="password"
                                name="password"
                                value={fields.password}
                                onChange={(e) => setField('password', e.target.value)}
                                required
                                className="mt-1 block w-full rounded-lg bg-white border-none px-3 py-2 text-gray-900 shadow-sm ring-1 ring-gray-50 ring-inset focus:outline-none focus:ring-2 focus:ring-indigo-200"
                            />
                        </div>

                        <div>
                            <label htmlFor="confirmPassword" className="block text-sm font-medium text-gray-700">Confirm password</label>
                            <input
                                type="password"
                                id="confirmPassword"
                                name="confirmPassword"
                                value={fields.confirmPassword}
                                onChange={(e) => setField('confirmPassword', e.target.value)}
                                required
                                className="mt-1 block w-full rounded-lg bg-white border-none px-3 py-2 text-gray-900 shadow-sm ring-1 ring-gray-50 ring-inset focus:outline-none focus:ring-2 focus:ring-indigo-200"
                            />
                            {errors.password && (
                                <p className="mt-2 text-sm text-red-700 bg-red-50 rounded-md px-3 py-1 shadow-md ring-1 ring-red-100 ring-inset">{errors.password}</p>
                            )}
                        </div>

                        <div>
                            <button
                                type="submit"
                                disabled={loading}
                                className="w-full inline-flex justify-center rounded-lg bg-white text-gray-900 font-medium py-2 px-4 shadow-md hover:shadow-lg transition-shadow ring-1 ring-gray-50 ring-inset disabled:opacity-60"
                            >
                                {loading ? 'Submitting...' : 'Reset password'}
                            </button>
                            {errors.server && (
                                <p className="mt-3 text-sm text-red-700 bg-red-50 rounded-md px-3 py-2 shadow-md ring-1 ring-red-100 ring-inset">{errors.server}</p>
                            )}
                        </div>
                    </div>
                </div>
            </div>
        </form>
    );
}