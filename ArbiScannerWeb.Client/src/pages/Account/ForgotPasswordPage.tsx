import { Link } from 'react-router';
import SuccessComponent from '../../components/SuccessComponent';
import { useForgotPasswordForm } from './hooks/useForgotPasswordForm';

export default function ForgotPasswordPage() {
    const { email, errors, loading, isSuccess, setEmail, handleSubmit } = useForgotPasswordForm();

    if (isSuccess) {
        return (
            <div className="min-h-[60vh] flex items-center justify-center px-4 py-8">
                <SuccessComponent message={`Link to restore password sent to ${email}`} title="Email Sent" />
            </div>
        );
    }

    return (
            <form onSubmit={handleSubmit} className="min-h-[60vh] flex items-center justify-center px-4 py-8">
                <div className="w-full max-w-md">
                    <div className="bg-white rounded-2xl shadow-md ring-1 ring-gray-50 ring-inset p-6">
                        <h2 className="text-2xl font-semibold text-gray-800 mb-4">Reset your password</h2>

                        <div className="space-y-4">
                            <div>
                                <label htmlFor="email" className="block text-sm font-medium text-gray-700">Email</label>
                                <input
                                    type="email"
                                    id="email"
                                    name="email"
                                    value={email}
                                    onChange={(e) => setEmail(e.target.value)}
                                    required
                                    aria-invalid={!!errors.email}
                                    className="mt-1 block w-full rounded-lg bg-white border-none px-3 py-2 text-gray-900 shadow-sm ring-1 ring-gray-50 ring-inset focus:outline-none focus:ring-2 focus:ring-indigo-200"
                                />
                                {errors.email && (
                                    <p className="mt-2 text-sm text-red-700 bg-red-50 rounded-md px-3 py-1 shadow-md ring-1 ring-red-100 ring-inset">{errors.email}</p>
                                )}
                            </div>

                            <div>
                                <button
                                    type="submit"
                                    disabled={loading}
                                    className="w-full inline-flex justify-center rounded-lg bg-white text-gray-900 font-medium py-2 px-4 shadow-md hover:shadow-lg transition-shadow ring-1 ring-gray-50 ring-inset disabled:opacity-60"
                                >
                                    {loading ? 'Sending...' : 'Send code'}
                                </button>
                                {errors.server && (
                                    <p className="mt-3 text-sm text-red-700 bg-red-50 rounded-md px-3 py-2 shadow-md ring-1 ring-red-100 ring-inset">{errors.server}</p>
                                )}
                            </div>
                            <div className="flex justify-between mt-3">
                                <Link to="/account/login" className="text-sm text-indigo-600 hover:underline">Sign in</Link>
                                <Link to="/account/register" className="text-sm text-indigo-600 hover:underline">Register</Link>
                            </div>
                        </div>
                    </div>
                </div>
            </form>
    );
}